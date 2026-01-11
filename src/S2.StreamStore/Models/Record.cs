using System.Text.Json;

namespace S2.StreamStore.Models;

/// <summary>
/// Format for reading records from a stream.
/// Determines how body and headers are decoded.
/// </summary>
public enum ReadFormat
{
    /// <summary>
    /// Decode body and headers as UTF-8 strings.
    /// </summary>
    String,

    /// <summary>
    /// Keep body and headers as binary (byte arrays).
    /// </summary>
    Bytes
}

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
    /// Headers of the record as key-value pairs.
    /// </summary>
    public IReadOnlyList<KeyValuePair<byte[], byte[]>>? Headers { get; init; }

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

    /// <summary>
    /// Get headers as string key-value pairs.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> GetHeadersAsStrings()
    {
        if (Headers == null) return Array.Empty<KeyValuePair<string, string>>();
        return Headers.Select(h => new KeyValuePair<string, string>(
            System.Text.Encoding.UTF8.GetString(h.Key),
            System.Text.Encoding.UTF8.GetString(h.Value)
        )).ToList();
    }
}

/// <summary>
/// A record read from an S2 stream with string body and headers.
/// </summary>
public sealed class StringRecord
{
    /// <summary>
    /// Sequence number of this record in the stream.
    /// </summary>
    public required long SequenceNumber { get; init; }

    /// <summary>
    /// Body of the record as a UTF-8 string.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>
    /// Timestamp when the record was appended to the stream.
    /// </summary>
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>
    /// Headers of the record as string key-value pairs.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>>? Headers { get; init; }

    /// <summary>
    /// Deserialize the body as JSON to the specified type.
    /// </summary>
    public T Deserialize<T>(JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<T>(Body, options)
            ?? throw new JsonException($"Failed to deserialize record body to {typeof(T).Name}");
    }

    /// <summary>
    /// Creates a StringRecord from a Record.
    /// </summary>
    public static StringRecord FromRecord(Record record) => new()
    {
        SequenceNumber = record.SequenceNumber,
        Body = record.GetBodyAsString(),
        Timestamp = record.Timestamp,
        Headers = record.GetHeadersAsStrings()
    };
}

/// <summary>
/// A record read from an S2 stream with binary body and headers.
/// Alias for Record with explicit naming to match TypeScript SDK patterns.
/// </summary>
public sealed class BytesRecord
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
    /// Headers of the record as binary key-value pairs.
    /// </summary>
    public IReadOnlyList<KeyValuePair<byte[], byte[]>>? Headers { get; init; }

    /// <summary>
    /// Creates a BytesRecord from a Record.
    /// </summary>
    public static BytesRecord FromRecord(Record record) => new()
    {
        SequenceNumber = record.SequenceNumber,
        Body = record.Body,
        Timestamp = record.Timestamp,
        Headers = record.Headers
    };
}
