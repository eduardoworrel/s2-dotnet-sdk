using System.Text.Json.Serialization;

namespace S2.StreamStore.Models;

/// <summary>
/// Information about an S2 stream.
/// </summary>
public sealed class StreamInfo
{
    /// <summary>
    /// Name of the stream.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Last modification timestamp.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Total number of records in the stream.
    /// </summary>
    [JsonPropertyName("record_count")]
    public long RecordCount { get; init; }

    /// <summary>
    /// Total size of the stream in bytes.
    /// </summary>
    [JsonPropertyName("total_size_bytes")]
    public long TotalSizeBytes { get; init; }

    /// <summary>
    /// First sequence number in the stream.
    /// </summary>
    [JsonPropertyName("first_sequence_number")]
    public long? FirstSequenceNumber { get; init; }

    /// <summary>
    /// Last sequence number in the stream.
    /// </summary>
    [JsonPropertyName("last_sequence_number")]
    public long? LastSequenceNumber { get; init; }
}

/// <summary>
/// Information about an S2 basin.
/// </summary>
public sealed class BasinInfo
{
    /// <summary>
    /// Name of the basin.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Basin state (active, deleted, etc).
    /// </summary>
    [JsonPropertyName("state")]
    public string? State { get; init; }
}
