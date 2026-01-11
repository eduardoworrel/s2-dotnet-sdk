using System.Threading.Channels;
using S2.StreamStore.Models;

namespace S2.StreamStore;

/// <summary>
/// Options for configuring the BatchTransform.
/// </summary>
public sealed class BatchTransformOptions
{
    /// <summary>
    /// Duration to wait before flushing a batch (default: 5ms).
    /// </summary>
    public TimeSpan LingerDuration { get; init; } = TimeSpan.FromMilliseconds(5);

    /// <summary>
    /// Maximum number of records in a batch (default: 1000, max: 1000).
    /// </summary>
    public int MaxBatchRecords { get; init; } = 1000;

    /// <summary>
    /// Maximum batch size in bytes (default: 1 MiB, max: 1 MiB).
    /// </summary>
    public int MaxBatchBytes { get; init; } = 1024 * 1024;

    /// <summary>
    /// Optional fencing token to enforce (remains static across batches).
    /// </summary>
    public string? FencingToken { get; init; }

    /// <summary>
    /// Optional starting sequence number to match (auto-increments for subsequent batches).
    /// </summary>
    public long? MatchSeqNum { get; init; }
}

/// <summary>
/// Output from the BatchTransform.
/// </summary>
public sealed class BatchOutput
{
    /// <summary>
    /// Records in this batch.
    /// </summary>
    public required List<AppendRecord> Records { get; init; }

    /// <summary>
    /// Optional fencing token.
    /// </summary>
    public string? FencingToken { get; init; }

    /// <summary>
    /// Optional sequence number to match.
    /// </summary>
    public long? MatchSeqNum { get; init; }

    /// <summary>
    /// Total metered bytes in this batch.
    /// </summary>
    public int MeteredBytes { get; init; }
}

/// <summary>
/// Batches AppendRecords based on time, record count, and byte size.
/// </summary>
public sealed class BatchTransform : IAsyncDisposable
{
    private readonly Channel<AppendRecord> _inputChannel;
    private readonly Channel<BatchOutput> _outputChannel;
    private readonly BatchTransformOptions _options;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _processTask;

    private List<AppendRecord> _currentBatch = [];
    private int _currentBatchSize;
    private long? _nextMatchSeqNum;
    private Timer? _lingerTimer;
    private readonly object _lock = new();

    /// <summary>
    /// Create a new BatchTransform with the specified options.
    /// </summary>
    public BatchTransform(BatchTransformOptions? options = null)
    {
        _options = options ?? new BatchTransformOptions();

        if (_options.MaxBatchRecords < 1 || _options.MaxBatchRecords > 1000)
            throw new ArgumentException("MaxBatchRecords must be between 1 and 1000", nameof(options));
        if (_options.MaxBatchBytes < 1 || _options.MaxBatchBytes > 1024 * 1024)
            throw new ArgumentException("MaxBatchBytes must be between 1 and 1 MiB", nameof(options));

        _nextMatchSeqNum = _options.MatchSeqNum;

        _inputChannel = Channel.CreateBounded<AppendRecord>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = false,
            SingleReader = true
        });

        _outputChannel = Channel.CreateUnbounded<BatchOutput>(new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = false
        });

        _processTask = ProcessAsync(_cts.Token);
    }

    /// <summary>
    /// Writer for submitting records to be batched.
    /// </summary>
    public ChannelWriter<AppendRecord> Writer => _inputChannel.Writer;

    /// <summary>
    /// Reader for consuming batched outputs.
    /// </summary>
    public ChannelReader<BatchOutput> Reader => _outputChannel.Reader;

    /// <summary>
    /// Write a record to be batched.
    /// </summary>
    public async ValueTask WriteAsync(AppendRecord record, CancellationToken ct = default)
    {
        await _inputChannel.Writer.WriteAsync(record, ct);
    }

    /// <summary>
    /// Complete the writer and wait for all batches to be flushed.
    /// </summary>
    public async Task CompleteAsync()
    {
        _inputChannel.Writer.Complete();
        await _processTask;
        _outputChannel.Writer.Complete();
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var record in _inputChannel.Reader.ReadAllAsync(ct))
            {
                HandleRecord(record);
            }

            // Flush any remaining records
            Flush();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Expected cancellation
        }
        finally
        {
            _lingerTimer?.Dispose();
        }
    }

    private void HandleRecord(AppendRecord record)
    {
        var recordSize = EstimateRecordSize(record);

        if (recordSize > _options.MaxBatchBytes)
            throw new InvalidOperationException($"Record size {recordSize} bytes exceeds maximum batch size of {_options.MaxBatchBytes} bytes");

        lock (_lock)
        {
            // Start linger timer on first record in empty batch
            if (_currentBatch.Count == 0 && _options.LingerDuration > TimeSpan.Zero)
            {
                StartLingerTimer();
            }

            // Check if adding this record would exceed limits
            var wouldExceedRecords = _currentBatch.Count + 1 > _options.MaxBatchRecords;
            var wouldExceedBytes = _currentBatchSize + recordSize > _options.MaxBatchBytes;

            if (wouldExceedRecords || wouldExceedBytes)
            {
                Flush();
                // Restart linger timer for new batch
                if (_options.LingerDuration > TimeSpan.Zero)
                {
                    StartLingerTimer();
                }
            }

            // Add record to current batch
            _currentBatch.Add(record);
            _currentBatchSize += recordSize;

            // Check if we've now reached the limits
            var nowExceedsRecords = _currentBatch.Count >= _options.MaxBatchRecords;
            var nowExceedsBytes = _currentBatchSize >= _options.MaxBatchBytes;

            if (nowExceedsRecords || nowExceedsBytes)
            {
                Flush();
            }
        }
    }

    private void Flush()
    {
        lock (_lock)
        {
            CancelLingerTimer();

            if (_currentBatch.Count == 0)
                return;

            var matchSeqNum = _nextMatchSeqNum;
            if (_nextMatchSeqNum.HasValue)
            {
                _nextMatchSeqNum += _currentBatch.Count;
            }

            var batch = new BatchOutput
            {
                Records = [.. _currentBatch],
                FencingToken = _options.FencingToken,
                MatchSeqNum = matchSeqNum,
                MeteredBytes = _currentBatchSize
            };

            _outputChannel.Writer.TryWrite(batch);

            _currentBatch = [];
            _currentBatchSize = 0;
        }
    }

    private void StartLingerTimer()
    {
        CancelLingerTimer();
        _lingerTimer = new Timer(_ =>
        {
            lock (_lock)
            {
                if (_currentBatch.Count > 0)
                {
                    Flush();
                }
            }
        }, null, _options.LingerDuration, Timeout.InfiniteTimeSpan);
    }

    private void CancelLingerTimer()
    {
        _lingerTimer?.Dispose();
        _lingerTimer = null;
    }

    private static int EstimateRecordSize(AppendRecord record)
    {
        // Estimate: body length + headers overhead
        var size = record.Body.Length;
        if (record.Headers != null)
        {
            foreach (var header in record.Headers)
            {
                size += header[0].Length + header[1].Length;
            }
        }
        return size;
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _lingerTimer?.Dispose();

        try
        {
            await _processTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        _cts.Dispose();
    }
}
