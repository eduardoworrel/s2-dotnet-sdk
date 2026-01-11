using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using S2.StreamStore.Models;

namespace S2.StreamStore.Sessions;

/// <summary>
/// High-throughput append session with automatic batching and pipelining.
/// </summary>
/// <example>
/// <code>
/// await using var session = stream.OpenAppendSession();
///
/// // Fire-and-forget appends (buffered)
/// for (int i = 0; i &lt; 10000; i++)
/// {
///     await session.AppendAsync(new { tick = i });
/// }
///
/// // Wait for all records to be sent
/// await session.FlushAsync();
/// </code>
/// </example>
public sealed class AppendSession : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly HttpClient _httpClient;
    private readonly AppendSessionOptions _options;
    private readonly Channel<PendingAppend> _queue;
    private readonly SemaphoreSlim _pipelineSemaphore;
    private readonly Task _writerTask;
    private readonly CancellationTokenSource _cts;
    private readonly JsonSerializerOptions _jsonOptions;

    private bool _disposed;
    private long _totalAppended;
    private long _totalSent;

    internal AppendSession(Stream stream, HttpClient httpClient, AppendSessionOptions options)
    {
        _stream = stream;
        _httpClient = httpClient;
        _options = options;
        _cts = new CancellationTokenSource();

        _queue = Channel.CreateBounded<PendingAppend>(new BoundedChannelOptions(options.BufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

        _pipelineSemaphore = new SemaphoreSlim(options.MaxConcurrentBatches);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        _writerTask = RunWriterLoopAsync();
    }

    /// <summary>
    /// Total records queued for appending.
    /// </summary>
    public long TotalAppended => Interlocked.Read(ref _totalAppended);

    /// <summary>
    /// Total records successfully sent to S2.
    /// </summary>
    public long TotalSent => Interlocked.Read(ref _totalSent);

    /// <summary>
    /// Append a record to the stream (buffered, non-blocking if buffer has space).
    /// </summary>
    public ValueTask<AppendReceipt> AppendAsync<T>(T data, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var json = JsonSerializer.Serialize(data, _jsonOptions);
        var body = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var pending = new PendingAppend(body);

        Interlocked.Increment(ref _totalAppended);

        // Try non-blocking write first
        if (_queue.Writer.TryWrite(pending))
        {
            return new ValueTask<AppendReceipt>(pending.Task);
        }

        // Async path if buffer is full
        return AppendSlowAsync(pending, ct);
    }

    private async ValueTask<AppendReceipt> AppendSlowAsync(PendingAppend pending, CancellationToken ct)
    {
        await _queue.Writer.WriteAsync(pending, ct);
        return await pending.Task;
    }

    /// <summary>
    /// Wait for all pending records to be sent.
    /// </summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        // Complete the writer to signal no more records
        _queue.Writer.Complete();

        // Wait for writer task to finish
        await _writerTask.WaitAsync(ct);
    }

    private async Task RunWriterLoopAsync()
    {
        var batch = new List<PendingAppend>();
        var batchTasks = new List<Task>();

        try
        {
            while (await _queue.Reader.WaitToReadAsync(_cts.Token))
            {
                // Collect batch
                batch.Clear();
                var deadline = DateTime.UtcNow + _options.BatchTimeout;

                while (batch.Count < _options.BatchSize)
                {
                    if (_queue.Reader.TryRead(out var item))
                    {
                        batch.Add(item);
                    }
                    else if (DateTime.UtcNow >= deadline || batch.Count > 0)
                    {
                        break;
                    }
                    else
                    {
                        // Wait a bit for more records
                        await Task.Delay(10, _cts.Token);
                    }
                }

                if (batch.Count > 0)
                {
                    // Wait for pipeline slot
                    await _pipelineSemaphore.WaitAsync(_cts.Token);

                    // Fire and forget batch (pipelining)
                    var batchCopy = batch.ToList();
                    var task = SendBatchAsync(batchCopy);
                    batchTasks.Add(task);

                    // Clean up completed tasks
                    batchTasks.RemoveAll(t => t.IsCompleted);
                }
            }

            // Wait for remaining batches
            if (batchTasks.Count > 0)
            {
                await Task.WhenAll(batchTasks);
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (ChannelClosedException)
        {
            // Channel completed - normal shutdown
        }
    }

    private async Task SendBatchAsync(List<PendingAppend> batch)
    {
        try
        {
            var request = new BatchAppendRequest
            {
                Records = batch.Select(p => new BatchAppendRecord { Body = p.Body }).ToList()
            };

            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(_options.RequestTimeout);
            var response = await _httpClient.PostAsync(_stream.RecordsUrl, content, cts.Token);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AppendResponse>(cts.Token);

            if (result != null)
            {
                // Complete all pending tasks with their sequence numbers
                for (int i = 0; i < batch.Count; i++)
                {
                    var receipt = new AppendReceipt
                    {
                        SequenceNumber = result.StartSequenceNumber + i,
                        Timestamp = DateTimeOffset.UtcNow
                    };
                    batch[i].SetResult(receipt);
                    Interlocked.Increment(ref _totalSent);
                }
            }
        }
        catch (Exception ex)
        {
            // Fail all pending tasks
            foreach (var item in batch)
            {
                item.SetException(ex);
            }
        }
        finally
        {
            _pipelineSemaphore.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _queue.Writer.TryComplete();
        _cts.Cancel();

        try
        {
            await _writerTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Ignore shutdown errors
        }

        _cts.Dispose();
        _pipelineSemaphore.Dispose();
    }

    private sealed class PendingAppend
    {
        private readonly TaskCompletionSource<AppendReceipt> _tcs;

        public PendingAppend(string body)
        {
            Body = body;
            _tcs = new TaskCompletionSource<AppendReceipt>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public string Body { get; }
        public Task<AppendReceipt> Task => _tcs.Task;

        public void SetResult(AppendReceipt receipt) => _tcs.TrySetResult(receipt);
        public void SetException(Exception ex) => _tcs.TrySetException(ex);
    }

    private sealed class BatchAppendRequest
    {
        public List<BatchAppendRecord> Records { get; set; } = [];
    }

    private sealed class BatchAppendRecord
    {
        public required string Body { get; set; }
    }
}
