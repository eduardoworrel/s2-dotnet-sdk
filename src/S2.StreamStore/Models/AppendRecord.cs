using System.Text;
using System.Text.Json.Serialization;

namespace S2.StreamStore.Models;

/// <summary>
/// A record to be appended to a stream.
/// </summary>
public class AppendRecord
{
    /// <summary>
    /// Record body (string or base64-encoded bytes).
    /// </summary>
    [JsonPropertyName("body")]
    public string Body { get; init; } = "";

    /// <summary>
    /// Record headers as [name, value] pairs.
    /// </summary>
    [JsonPropertyName("headers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string[]>? Headers { get; init; }

    /// <summary>
    /// Optional timestamp (milliseconds since Unix epoch).
    /// </summary>
    [JsonPropertyName("timestamp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Timestamp { get; init; }

    /// <summary>
    /// Create a string append record.
    /// </summary>
    /// <param name="body">The record body.</param>
    /// <param name="headers">Optional headers as [name, value] pairs.</param>
    /// <param name="timestamp">Optional timestamp.</param>
    public static AppendRecord String(
        string body,
        IEnumerable<(string Name, string Value)>? headers = null,
        DateTimeOffset? timestamp = null)
    {
        return new AppendRecord
        {
            Body = body,
            Headers = headers?.Select(h => new[] { h.Name, h.Value }).ToList(),
            Timestamp = timestamp?.ToUnixTimeMilliseconds()
        };
    }

    /// <summary>
    /// Create a binary append record with base64-encoded body.
    /// </summary>
    /// <param name="body">The record body as bytes.</param>
    /// <param name="headers">Optional headers as [name, value] pairs (bytes will be base64-encoded).</param>
    /// <param name="timestamp">Optional timestamp.</param>
    public static AppendRecord Bytes(
        byte[] body,
        IEnumerable<(byte[] Name, byte[] Value)>? headers = null,
        DateTimeOffset? timestamp = null)
    {
        return new AppendRecord
        {
            Body = Convert.ToBase64String(body),
            Headers = headers?.Select(h => new[]
            {
                Convert.ToBase64String(h.Name),
                Convert.ToBase64String(h.Value)
            }).ToList(),
            Timestamp = timestamp?.ToUnixTimeMilliseconds()
        };
    }

    /// <summary>
    /// Create a fence command record.
    /// Sets a fencing token that subsequent appends must match.
    /// </summary>
    /// <param name="fencingToken">The fencing token to set.</param>
    /// <param name="timestamp">Optional timestamp.</param>
    public static AppendRecord Fence(string fencingToken, DateTimeOffset? timestamp = null)
    {
        return String(
            body: fencingToken,
            headers: [("", "fence")],
            timestamp: timestamp);
    }

    /// <summary>
    /// Create a trim command record.
    /// Marks all records before the specified sequence number for deletion.
    /// </summary>
    /// <param name="seqNum">The sequence number to trim to (records before this will be deleted).</param>
    /// <param name="timestamp">Optional timestamp.</param>
    public static AppendRecord Trim(long seqNum, DateTimeOffset? timestamp = null)
    {
        // Encode seqNum as big-endian 64-bit unsigned integer
        var buffer = new byte[8];
        var value = (ulong)seqNum;
        for (int i = 7; i >= 0; i--)
        {
            buffer[i] = (byte)(value & 0xFF);
            value >>= 8;
        }

        return Bytes(
            body: buffer,
            headers: [([], Encoding.UTF8.GetBytes("trim"))],
            timestamp: timestamp);
    }
}

/// <summary>
/// Input for append operations with optional fencing token and match sequence number.
/// </summary>
public class AppendInput
{
    /// <summary>
    /// Records to append.
    /// </summary>
    [JsonPropertyName("records")]
    public List<AppendRecord> Records { get; init; } = [];

    /// <summary>
    /// Optional fencing token to enforce.
    /// </summary>
    [JsonPropertyName("fencing_token")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FencingToken { get; init; }

    /// <summary>
    /// Optional sequence number to match (for optimistic concurrency).
    /// </summary>
    [JsonPropertyName("match_seq_num")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? MatchSeqNum { get; init; }

    /// <summary>
    /// Create an AppendInput with the specified records.
    /// </summary>
    public static AppendInput Create(
        IEnumerable<AppendRecord> records,
        string? fencingToken = null,
        long? matchSeqNum = null)
    {
        var recordList = records.ToList();
        if (recordList.Count == 0)
            throw new ArgumentException("AppendInput must contain at least one record", nameof(records));
        if (recordList.Count > 1000)
            throw new ArgumentException("AppendInput cannot contain more than 1000 records", nameof(records));

        return new AppendInput
        {
            Records = recordList,
            FencingToken = fencingToken,
            MatchSeqNum = matchSeqNum
        };
    }
}

/// <summary>
/// Response from an append operation.
/// </summary>
public sealed class AppendResponse
{
    /// <summary>
    /// Start sequence number of the appended records.
    /// </summary>
    [JsonPropertyName("start_seq_num")]
    public long StartSequenceNumber { get; init; }

    /// <summary>
    /// End sequence number of the appended records (exclusive).
    /// </summary>
    [JsonPropertyName("end_seq_num")]
    public long EndSequenceNumber { get; init; }

    /// <summary>
    /// Start timestamp.
    /// </summary>
    [JsonPropertyName("start_timestamp")]
    public long? StartTimestamp { get; init; }

    /// <summary>
    /// End timestamp.
    /// </summary>
    [JsonPropertyName("end_timestamp")]
    public long? EndTimestamp { get; init; }

    /// <summary>
    /// Current tail position.
    /// </summary>
    [JsonPropertyName("tail")]
    public StreamPosition? Tail { get; init; }
}

/// <summary>
/// Acknowledgment from an append operation (richer format).
/// </summary>
public sealed class AppendAck
{
    /// <summary>
    /// Start position of appended records.
    /// </summary>
    [JsonPropertyName("start")]
    public StreamPosition? Start { get; init; }

    /// <summary>
    /// End position of appended records.
    /// </summary>
    [JsonPropertyName("end")]
    public StreamPosition? End { get; init; }

    /// <summary>
    /// Current tail position.
    /// </summary>
    [JsonPropertyName("tail")]
    public StreamPosition? Tail { get; init; }
}

/// <summary>
/// Receipt from appending a single record.
/// </summary>
public sealed class AppendReceipt
{
    /// <summary>
    /// Sequence number assigned to the record.
    /// </summary>
    public long SequenceNumber { get; init; }

    /// <summary>
    /// Timestamp when the record was written.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; }
}
