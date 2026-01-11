namespace S2.StreamStore.Sessions;

/// <summary>
/// Options for configuring a read session.
/// </summary>
public sealed class ReadSessionOptions
{
    /// <summary>
    /// Where to start reading from. Defaults to tail (latest record).
    /// </summary>
    public ReadStart Start { get; set; } = ReadStart.FromTail(1);

    /// <summary>
    /// Maximum number of records to read before stopping.
    /// Null means read indefinitely (streaming mode).
    /// </summary>
    public long? MaxRecords { get; set; }

    /// <summary>
    /// Timeout for individual read operations.
    /// </summary>
    public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Specifies where to start reading from in a stream.
/// </summary>
public abstract class ReadStart
{
    internal abstract string ToQueryParam();

    /// <summary>
    /// Start reading from the tail of the stream (most recent records).
    /// </summary>
    /// <param name="offset">Number of records from the end. 1 = last record, 0 = wait for next.</param>
    public static ReadStart FromTail(long offset = 1) => new TailReadStart(offset);

    /// <summary>
    /// Start reading from a specific sequence number.
    /// </summary>
    public static ReadStart FromSequence(long sequenceNumber) => new SequenceReadStart(sequenceNumber);

    /// <summary>
    /// Start reading from the beginning of the stream.
    /// </summary>
    public static ReadStart FromBeginning() => new SequenceReadStart(0);

    /// <summary>
    /// Start reading from a specific timestamp.
    /// </summary>
    public static ReadStart FromTimestamp(DateTimeOffset timestamp) => new TimestampReadStart(timestamp);

    private sealed class TailReadStart(long offset) : ReadStart
    {
        internal override string ToQueryParam() => $"tail_offset={offset}";
    }

    private sealed class SequenceReadStart(long sequence) : ReadStart
    {
        internal override string ToQueryParam() => $"start_seq={sequence}";
    }

    private sealed class TimestampReadStart(DateTimeOffset timestamp) : ReadStart
    {
        internal override string ToQueryParam() => $"start_ts={timestamp.ToUnixTimeMilliseconds()}";
    }
}
