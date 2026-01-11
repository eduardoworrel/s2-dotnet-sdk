namespace S2.StreamStore.Sessions;

/// <summary>
/// Options for configuring an append session.
/// </summary>
public sealed class AppendSessionOptions
{
    /// <summary>
    /// Maximum records per batch. Defaults to 100.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum time to wait before flushing a partial batch.
    /// Defaults to 50ms.
    /// </summary>
    public TimeSpan BatchTimeout { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Maximum number of concurrent batches being sent (pipelining).
    /// Defaults to 4.
    /// </summary>
    public int MaxConcurrentBatches { get; set; } = 4;

    /// <summary>
    /// Size of the internal buffer. Defaults to 10000 records.
    /// </summary>
    public int BufferSize { get; set; } = 10000;

    /// <summary>
    /// HTTP timeout for individual batch requests.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
