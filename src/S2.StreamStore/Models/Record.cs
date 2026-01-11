using System.Text.Json;

namespace S2.StreamStore.Models;

/// <summary>
/// A record read from an S2 stream.
/// </summary>
public sealed class Record
{
    /// <summary>
    /// Sequence number of this record in the stream.
    /// </summary>
    public required long SequenceNumber { get; init; }

    /// <summary>
    /// Raw body bytes of the record.
    /// </summary>
    public required byte[] Body { get; init; }

    /// <summary>
    /// Timestamp when the record was appended to the stream.
    /// </summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>
    /// Deserialize the body as JSON to the specified type.
    /// </summary>
    public T Deserialize<T>(JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<T>(Body, options)
            ?? throw new JsonException($"Failed to deserialize record body to {typeof(T).Name}");
    }

    /// <summary>
    /// Get the body as a UTF-8 string.
    /// </summary>
    public string GetBodyAsString() => System.Text.Encoding.UTF8.GetString(Body);
}

/// <summary>
/// Receipt returned after successfully appending a record.
/// </summary>
public sealed class AppendReceipt
{
    /// <summary>
    /// Sequence number assigned to the appended record.
    /// </summary>
    public required long SequenceNumber { get; init; }

    /// <summary>
    /// Timestamp when the record was appended.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Batch append response from S2.
/// </summary>
public sealed class AppendResponse
{
    /// <summary>
    /// Starting sequence number for the batch.
    /// </summary>
    public long StartSequenceNumber { get; set; }

    /// <summary>
    /// Ending sequence number for the batch (exclusive).
    /// </summary>
    public long EndSequenceNumber { get; set; }
}
