using System.Text.Json.Serialization;

namespace S2.StreamStore.Models;

/// <summary>
/// Position of a record in a stream.
/// </summary>
public sealed class StreamPosition
{
    /// <summary>
    /// Sequence number assigned by the service.
    /// </summary>
    [JsonPropertyName("seq_num")]
    public long SeqNum { get; init; }

    /// <summary>
    /// Timestamp of the record (milliseconds since Unix epoch).
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    /// <summary>
    /// Timestamp as DateTimeOffset.
    /// </summary>
    [JsonIgnore]
    public DateTimeOffset TimestampDate => DateTimeOffset.FromUnixTimeMilliseconds(Timestamp);
}

/// <summary>
/// Response from checking the tail of a stream.
/// </summary>
public sealed class TailResponse
{
    /// <summary>
    /// The tail position (next sequence number to be assigned).
    /// </summary>
    [JsonPropertyName("tail")]
    public StreamPosition? Tail { get; init; }
}

/// <summary>
/// Information about an S2 stream.
/// </summary>
public sealed class StreamInfo
{
    /// <summary>
    /// Stream name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Storage class of the stream.
    /// </summary>
    [JsonPropertyName("storage_class")]
    public string? StorageClass { get; init; }

    /// <summary>
    /// Retention policy for the stream.
    /// </summary>
    [JsonPropertyName("retention_policy")]
    public RetentionPolicy? RetentionPolicy { get; init; }

    /// <summary>
    /// Timestamping configuration.
    /// </summary>
    [JsonPropertyName("timestamping")]
    public TimestampingConfig? Timestamping { get; init; }

    /// <summary>
    /// Delete-on-empty configuration.
    /// </summary>
    [JsonPropertyName("delete_on_empty")]
    public DeleteOnEmptyConfig? DeleteOnEmpty { get; init; }

    /// <summary>
    /// Creation time.
    /// </summary>
    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; init; }

    /// <summary>
    /// Deletion time, if being deleted.
    /// </summary>
    [JsonPropertyName("deleted_at")]
    public string? DeletedAt { get; init; }

    /// <summary>
    /// First sequence number in the stream.
    /// </summary>
    [JsonPropertyName("first_seq_num")]
    public long? FirstSeqNum { get; init; }

    /// <summary>
    /// Next sequence number to be written.
    /// </summary>
    [JsonPropertyName("next_seq_num")]
    public long? NextSeqNum { get; init; }
}

/// <summary>
/// Stream configuration.
/// </summary>
public sealed class StreamConfig
{
    /// <summary>
    /// Storage class.
    /// </summary>
    [JsonPropertyName("storage_class")]
    public string? StorageClass { get; init; }

    /// <summary>
    /// Retention policy.
    /// </summary>
    [JsonPropertyName("retention_policy")]
    public RetentionPolicy? RetentionPolicy { get; init; }

    /// <summary>
    /// Timestamping configuration.
    /// </summary>
    [JsonPropertyName("timestamping")]
    public TimestampingConfig? Timestamping { get; init; }

    /// <summary>
    /// Delete-on-empty configuration.
    /// </summary>
    [JsonPropertyName("delete_on_empty")]
    public DeleteOnEmptyConfig? DeleteOnEmpty { get; init; }
}

/// <summary>
/// Retention policy configuration.
/// </summary>
public sealed class RetentionPolicy
{
    /// <summary>
    /// Age in seconds after which records are deleted.
    /// </summary>
    [JsonPropertyName("age")]
    public long? Age { get; init; }

    /// <summary>
    /// Infinite retention marker.
    /// </summary>
    [JsonPropertyName("infinite")]
    public object? Infinite { get; init; }
}

/// <summary>
/// Timestamping configuration.
/// </summary>
public sealed class TimestampingConfig
{
    /// <summary>
    /// Timestamping mode (e.g., "client-prefer", "arrival").
    /// </summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    /// <summary>
    /// Whether timestamps are uncapped.
    /// </summary>
    [JsonPropertyName("uncapped")]
    public bool Uncapped { get; init; }
}

/// <summary>
/// Delete-on-empty configuration.
/// </summary>
public sealed class DeleteOnEmptyConfig
{
    /// <summary>
    /// Minimum age in seconds before an empty stream can be deleted.
    /// </summary>
    [JsonPropertyName("min_age_secs")]
    public long? MinAgeSecs { get; init; }
}

/// <summary>
/// Information about an S2 basin.
/// </summary>
public sealed class BasinInfo
{
    /// <summary>
    /// Basin name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Basin scope.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    /// <summary>
    /// Basin state (active, deleting, etc).
    /// </summary>
    [JsonPropertyName("state")]
    public string? State { get; init; }
}

/// <summary>
/// Basin configuration.
/// </summary>
public sealed class BasinConfig
{
    /// <summary>
    /// Create stream on append if it doesn't exist.
    /// </summary>
    [JsonPropertyName("create_stream_on_append")]
    public bool? CreateStreamOnAppend { get; init; }

    /// <summary>
    /// Create stream on read if it doesn't exist.
    /// </summary>
    [JsonPropertyName("create_stream_on_read")]
    public bool? CreateStreamOnRead { get; init; }

    /// <summary>
    /// Default stream configuration.
    /// </summary>
    [JsonPropertyName("default_stream_config")]
    public StreamConfig? DefaultStreamConfig { get; init; }
}

/// <summary>
/// Response from listing streams.
/// </summary>
public sealed class ListStreamsResponse
{
    /// <summary>
    /// List of streams.
    /// </summary>
    [JsonPropertyName("streams")]
    public List<StreamInfo> Streams { get; init; } = [];

    /// <summary>
    /// Whether there are more results.
    /// </summary>
    [JsonPropertyName("has_more")]
    public bool HasMore { get; init; }
}

/// <summary>
/// Response from listing basins.
/// </summary>
public sealed class ListBasinsResponse
{
    /// <summary>
    /// List of basins.
    /// </summary>
    [JsonPropertyName("basins")]
    public List<BasinInfo> Basins { get; init; } = [];

    /// <summary>
    /// Whether there are more results.
    /// </summary>
    [JsonPropertyName("has_more")]
    public bool HasMore { get; init; }
}

/// <summary>
/// Input for creating a stream.
/// </summary>
public sealed class CreateStreamInput
{
    /// <summary>
    /// Stream name.
    /// </summary>
    [JsonPropertyName("stream")]
    public required string Stream { get; init; }

    /// <summary>
    /// Stream configuration.
    /// </summary>
    [JsonPropertyName("config")]
    public StreamConfig? Config { get; init; }
}

/// <summary>
/// Input for creating a basin.
/// </summary>
public sealed class CreateBasinInput
{
    /// <summary>
    /// Basin name (8-48 chars, globally unique).
    /// </summary>
    [JsonPropertyName("basin")]
    public required string Basin { get; init; }

    /// <summary>
    /// Basin configuration.
    /// </summary>
    [JsonPropertyName("config")]
    public BasinConfig? Config { get; init; }

    /// <summary>
    /// Basin scope.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; init; }
}

/// <summary>
/// Access token information.
/// </summary>
public sealed class AccessTokenInfo
{
    /// <summary>
    /// Access token ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>
    /// Access token scope.
    /// </summary>
    [JsonPropertyName("scope")]
    public AccessTokenScope? Scope { get; init; }

    /// <summary>
    /// Whether streams are auto-prefixed.
    /// </summary>
    [JsonPropertyName("auto_prefix_streams")]
    public bool? AutoPrefixStreams { get; init; }

    /// <summary>
    /// Expiration time.
    /// </summary>
    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; init; }
}

/// <summary>
/// Access token scope.
/// </summary>
public sealed class AccessTokenScope
{
    /// <summary>
    /// Resource set for access tokens.
    /// </summary>
    [JsonPropertyName("access_tokens")]
    public ResourceSet? AccessTokens { get; init; }

    /// <summary>
    /// Resource set for basins.
    /// </summary>
    [JsonPropertyName("basins")]
    public ResourceSet? Basins { get; init; }

    /// <summary>
    /// Resource set for streams.
    /// </summary>
    [JsonPropertyName("streams")]
    public ResourceSet? Streams { get; init; }

    /// <summary>
    /// Permitted operation groups.
    /// </summary>
    [JsonPropertyName("op_groups")]
    public List<string>? OpGroups { get; init; }

    /// <summary>
    /// Operations allowed.
    /// </summary>
    [JsonPropertyName("ops")]
    public List<string>? Ops { get; init; }
}

/// <summary>
/// Resource set for scoping.
/// </summary>
public sealed class ResourceSet
{
    /// <summary>
    /// Exact match.
    /// </summary>
    [JsonPropertyName("exact")]
    public string? Exact { get; init; }

    /// <summary>
    /// Prefix match.
    /// </summary>
    [JsonPropertyName("prefix")]
    public string? Prefix { get; init; }

    /// <summary>
    /// All resources.
    /// </summary>
    [JsonPropertyName("all")]
    public object? All { get; init; }
}

/// <summary>
/// Response from listing access tokens.
/// </summary>
public sealed class ListAccessTokensResponse
{
    /// <summary>
    /// List of access tokens.
    /// </summary>
    [JsonPropertyName("access_tokens")]
    public List<AccessTokenInfo> AccessTokens { get; init; } = [];

    /// <summary>
    /// Whether there are more results.
    /// </summary>
    [JsonPropertyName("has_more")]
    public bool HasMore { get; init; }
}

/// <summary>
/// Input for issuing an access token.
/// </summary>
public sealed class IssueAccessTokenInput
{
    /// <summary>
    /// Access token ID (unique, 1-96 bytes).
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// Access token scope.
    /// </summary>
    [JsonPropertyName("scope")]
    public required AccessTokenScope Scope { get; init; }

    /// <summary>
    /// Auto-prefix streams.
    /// </summary>
    [JsonPropertyName("auto_prefix_streams")]
    public bool? AutoPrefixStreams { get; init; }

    /// <summary>
    /// Expiration time (RFC 3339).
    /// </summary>
    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; init; }
}

/// <summary>
/// Response from issuing an access token.
/// </summary>
public sealed class IssueAccessTokenResponse
{
    /// <summary>
    /// The created access token.
    /// </summary>
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }
}
