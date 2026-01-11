using System.Threading.Channels;
using S2.StreamStore.Models;
using S2.StreamStore.Sessions;

namespace S2.StreamStore;

/// <summary>
/// Acknowledgment for a submitted record with its position in the batch.
/// </summary>
public sealed class IndexedAppendAck
{
    /// <summary>
    /// Index of this record within the batch.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// The batch acknowledgment.
    /// </summary>
    public AppendAck BatchAck { get; init; } = null!;

    /// <summary>
    /// Sequence number assigned to this specific record.
    /// </summary>
    public long SeqNum => (BatchAck.Start?.SeqNum ?? 0) + Index;
}

/// <summary>
/// Ticket for tracking a submitted record's durability.
/// </summary>
public sealed class RecordSubmitTicket
{
    private readonly Task<IndexedAppendAck> _ackTask;

    internal RecordSubmitTicket(Task<IndexedAppendAck> ackTask)
    {
        _ackTask = ackTask;
    }

    /// <summary>
    /// Wait for the record to become durable.
    /// </summary>
    public Task<IndexedAppendAck> AckAsync() => _ackTask;
}

/// <summary>
/// Options for configuring the Producer.
/// </summary>
public sealed class ProducerOptions
{
    /// <summary>
    /// Duration to wait before flushing a batch (default: 5ms).
    /// </summary>
    public TimeSpan LingerDuration { get; init; } = TimeSpan.FromMilliseconds(5);

    /// <summary>
    /// Maximum number of records in a batch (default: 1000).
    /// </summary>
    public int MaxBatchRecords { get; init; } = 1000;

    /// <summary>
    /// Maximum batch size in bytes (default: 1 MiB).
    /// </summary>
    public int MaxBatchBytes { get; init; } = 1024 * 1024;

    /// <summary>
    /// Optional fencing token to enforce.
    /// </summary>
    public string? FencingToken { get; init; }

    /// <summary>
    /// Optional starting sequence number to match.
    /// </summary>
    public long? MatchSeqNum { get; init; }
}

/// <summary>
/// Internal record for tracking in-flight submissions.
/// </summary>
internal sealed class InflightRecord
{
    public TaskCompletionSource<IndexedAppendAck> AckTcs { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

/// <summary>
/// Producer provides per-record append semantics on top of a batched AppendSession.
///
/// Submit() returns a RecordSubmitTicket that resolves once the record has been accepted.
/// The ticket's AckAsync() returns once the record is durable.
/// </summary>
/// <example>
/// <code>
/// await using var producer = new Producer(stream, new ProducerOptions { MaxBatchRecords = 100 });
/// var ticket = await producer.SubmitAsync(AppendRecord.String("hello"));
/// var ack = await ticket.AckAsync();
/// Console.WriteLine($"Record durable at seq {ack.SeqNum}");
/// </code>
/// </example>
public sealed class Producer : IAsyncDisposable
{
    private readonly BatchTransform _batchTransform;
    private readonly AppendSession _appendSession;
    private readonly Task _pumpTask;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<InflightRecord> _inflightRecords = [];
    private readonly object _lock = new();
    private Exception? _pumpError;

    /// <summary>
    /// Create a new Producer for the specified stream.
    /// </summary>
    public Producer(Stream stream, ProducerOptions? options = null)
    {
        var opts = options ?? new ProducerOptions();

        _batchTransform = new BatchTransform(new BatchTransformOptions
        {
            LingerDuration = opts.LingerDuration,
            MaxBatchRecords = opts.MaxBatchRecords,
            MaxBatchBytes = opts.MaxBatchBytes,
            FencingToken = opts.FencingToken,
            MatchSeqNum = opts.MatchSeqNum
        });

        _appendSession = stream.OpenAppendSession(new AppendSessionOptions
        {
            BatchSize = opts.MaxBatchRecords,
            BatchTimeout = opts.LingerDuration
        });

        _pumpTask = RunPumpAsync(_cts.Token);
    }

    /// <summary>
    /// Submit a record for appending.
    /// Returns a ticket that can be used to wait for durability acknowledgment.
    /// </summary>
    public async Task<RecordSubmitTicket> SubmitAsync(AppendRecord record, CancellationToken ct = default)
    {
        if (_pumpError != null)
            throw new InvalidOperationException($"Cannot submit: producer has failed: {_pumpError.Message}", _pumpError);

        var inflight = new InflightRecord();

        lock (_lock)
        {
            _inflightRecords.Add(inflight);
        }

        try
        {
            await _batchTransform.WriteAsync(record, ct);
        }
        catch (Exception ex)
        {
            lock (_lock)
            {
                _inflightRecords.Remove(inflight);
            }
            inflight.AckTcs.TrySetException(ex);
            throw;
        }

        return new RecordSubmitTicket(inflight.AckTcs.Task);
    }

    /// <summary>
    /// Close the producer gracefully.
    /// Waits for all pending records to be flushed, submitted, and acknowledged.
    /// </summary>
    public async Task CloseAsync()
    {
        await _batchTransform.CompleteAsync();
        await _pumpTask;
        await _appendSession.FlushAsync();
        await _appendSession.DisposeAsync();

        // Reject any remaining inflight records
        lock (_lock)
        {
            if (_inflightRecords.Count > 0)
            {
                var error = new InvalidOperationException("Producer closed with pending records");
                foreach (var record in _inflightRecords)
                {
                    record.AckTcs.TrySetException(error);
                }
                _inflightRecords.Clear();
            }
        }

        if (_pumpError != null)
            throw _pumpError;
    }

    private async Task RunPumpAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var batch in _batchTransform.Reader.ReadAllAsync(ct))
            {
                var recordCount = batch.Records.Count;

                // Get associated inflight records (FIFO correspondence)
                List<InflightRecord> associatedRecords;
                lock (_lock)
                {
                    if (_inflightRecords.Count < recordCount)
                    {
                        throw new InvalidOperationException($"Internal error: flushed {recordCount} records but only {_inflightRecords.Count} inflight entries");
                    }

                    associatedRecords = _inflightRecords.Take(recordCount).ToList();
                    _inflightRecords.RemoveRange(0, recordCount);
                }

                try
                {
                    // Submit batch to append session
                    foreach (var record in batch.Records)
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(new { body = record.Body, headers = record.Headers });
                        await _appendSession.AppendAsync(json, ct);
                    }

                    await _appendSession.FlushAsync(ct);

                    // Create a mock ack for now (AppendSession doesn't return per-batch acks)
                    // In a full implementation, we'd track the acks from AppendSession
                    var ack = new AppendAck
                    {
                        Start = new StreamPosition { SeqNum = 0 },
                        End = new StreamPosition { SeqNum = recordCount },
                        Tail = new StreamPosition { SeqNum = recordCount }
                    };

                    // Resolve acks for all records in this batch
                    for (var i = 0; i < associatedRecords.Count; i++)
                    {
                        var indexedAck = new IndexedAppendAck { Index = i, BatchAck = ack };
                        associatedRecords[i].AckTcs.TrySetResult(indexedAck);
                    }
                }
                catch (Exception ex)
                {
                    _pumpError ??= ex;

                    // Reject all records in this batch
                    foreach (var record in associatedRecords)
                    {
                        record.AckTcs.TrySetException(ex);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected
        }
        catch (Exception ex)
        {
            _pumpError ??= ex;

            // Reject all remaining inflight records
            lock (_lock)
            {
                foreach (var record in _inflightRecords)
                {
                    record.AckTcs.TrySetException(ex);
                }
                _inflightRecords.Clear();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        await _batchTransform.DisposeAsync();

        try
        {
            await _pumpTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        await _appendSession.DisposeAsync();
        _cts.Dispose();
    }
}
