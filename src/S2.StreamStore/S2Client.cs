using S2.StreamStore.Http;
using S2.StreamStore.Models;

namespace S2.StreamStore;

/// <summary>
/// Main entry point for the S2 StreamStore SDK.
/// </summary>
/// <example>
/// <code>
/// var s2 = new S2Client(new S2Options { AccessToken = "s2_token_..." });
/// var basin = s2.Basin("my-basin");
/// var stream = basin.Stream("my-stream");
/// await stream.AppendAsync(new { message = "hello" });
/// </code>
/// </example>
public sealed class S2Client : IDisposable
{
    private readonly S2Options _options;
    private readonly S2HttpClient _httpClient;
    private readonly Lazy<S2Basins> _basins;
    private readonly Lazy<S2AccessTokens> _accessTokens;
    private readonly Lazy<S2Metrics> _metrics;
    private bool _disposed;

    /// <summary>
    /// Create a new S2 client with the specified options.
    /// </summary>
    public S2Client(S2Options options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.AccessToken);

        _options = options;
        _httpClient = new S2HttpClient(options);
        _basins = new Lazy<S2Basins>(() => new S2Basins(_httpClient, _options.GetAccountUrl()));
        _accessTokens = new Lazy<S2AccessTokens>(() => new S2AccessTokens(_httpClient, _options.GetAccountUrl()));
        _metrics = new Lazy<S2Metrics>(() => new S2Metrics(_httpClient, _options.GetAccountUrl()));
    }

    /// <summary>
    /// Account-scoped basin management operations (list, create, delete, reconfigure).
    /// </summary>
    public S2Basins Basins => _basins.Value;

    /// <summary>
    /// Account-scoped access token management (list, issue, revoke).
    /// </summary>
    public S2AccessTokens AccessTokens => _accessTokens.Value;

    /// <summary>
    /// Account and basin level metrics access.
    /// </summary>
    public S2Metrics Metrics => _metrics.Value;

    /// <summary>
    /// Get a basin reference by name.
    /// Does not verify the basin exists - use GetInfoAsync() to check.
    /// </summary>
    public Basin Basin(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Basin(name, _options, _httpClient);
    }

    /// <summary>
    /// List all basins accessible with the current token.
    /// </summary>
    [Obsolete("Use s2.Basins.ListAsync() instead")]
    public async Task<IReadOnlyList<BasinInfo>> ListBasinsAsync(CancellationToken ct = default)
    {
        var response = await Basins.ListAsync(ct: ct);
        return response.Basins;
    }

    /// <summary>
    /// Create a new access token with limited scope.
    /// </summary>
    [Obsolete("Use s2.AccessTokens.IssueAsync() instead")]
    public async Task<AccessTokenResponse> CreateAccessTokenAsync(
        AccessTokenRequest request,
        CancellationToken ct = default)
    {
        var url = $"{_options.GetAccountUrl()}/access_tokens";
        return await _httpClient.PostAsync<AccessTokenRequest, AccessTokenResponse>(url, request, ct);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }
}

/// <summary>
/// Request to create a scoped access token.
/// </summary>
public sealed class AccessTokenRequest
{
    /// <summary>
    /// Basin scope filter.
    /// </summary>
    public ScopeFilter? Basins { get; set; }

    /// <summary>
    /// Stream scope filter.
    /// </summary>
    public ScopeFilter? Streams { get; set; }

    /// <summary>
    /// Allowed operations.
    /// </summary>
    public List<string> Operations { get; set; } = ["read", "write"];

    /// <summary>
    /// Token expiration time.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }
}

/// <summary>
/// Scope filter for access tokens.
/// </summary>
public sealed class ScopeFilter
{
    /// <summary>
    /// Exact match.
    /// </summary>
    public string? Exact { get; set; }

    /// <summary>
    /// Prefix match.
    /// </summary>
    public string? Prefix { get; set; }
}

/// <summary>
/// Response containing the created access token.
/// </summary>
public sealed class AccessTokenResponse
{
    /// <summary>
    /// The access token string.
    /// </summary>
    public required string AccessToken { get; set; }

    /// <summary>
    /// When the token expires.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }
}
