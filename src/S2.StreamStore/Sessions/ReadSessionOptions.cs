using S2.StreamStore.Models;

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

    /// <summary>
    /// Start from tail if requested position is beyond it.
    /// When true, prevents RangeNotSatisfiableException by clamping to the tail.
    /// </summary>
    public bool Clamp { get; set; } = false;

    /// <summary>
    /// Format for reading records.
    /// Determines how body and headers are decoded.
    /// Default: Bytes (raw binary)
    /// </summary>
    public ReadFormat Format { get; set; } = ReadFormat.Bytes;

    /// <summary>
    /// Whether to skip command records (fence, trim) when reading.
    /// Default: false (include all records)
    /// </summary>
    public bool IgnoreCommandRecords { get; set; } = false;

    /// <summary>
    /// Duration in seconds to wait for new records before stopping.
    /// Only applicable for non-streaming reads.
    /// </summary>
    public int? WaitSecs { get; set; }

    /// <summary>
    /// Timestamp at which to stop reading (exclusive).
    /// </summary>
    public DateTimeOffset? UntilTimestamp { get; set; }
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
