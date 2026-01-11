namespace S2.StreamStore;

/// <summary>
/// Policy for retrying append operations.
/// </summary>
public enum AppendRetryPolicy
{
    /// <summary>
    /// Retry all append operations, including those that may have side effects (default).
    /// </summary>
    All,

    /// <summary>
    /// Only retry append operations that are guaranteed to have no side effects.
    /// </summary>
    NoSideEffects
}

/// <summary>
/// Retry configuration for handling transient failures.
/// </summary>
public sealed class RetryConfig
{
    /// <summary>
    /// Total number of attempts, including the initial try.
    /// Must be >= 1. A value of 1 means no retries.
    /// Default: 3
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Minimum delay for exponential backoff.
    /// The first retry will have a delay in the range [MinDelay, 2*MinDelay).
    /// Default: 100ms
    /// </summary>
    public TimeSpan MinDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Maximum base delay for exponential backoff.
    /// Once the exponential backoff reaches this value, it stays capped here.
    /// Note: actual delay with jitter can be up to 2*MaxDelay.
    /// Default: 1000ms
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMilliseconds(1000);

    /// <summary>
    /// Policy for retrying append operations.
    /// Default: All
    /// </summary>
    public AppendRetryPolicy AppendRetryPolicy { get; set; } = AppendRetryPolicy.All;

    /// <summary>
    /// Maximum time to wait for an append ack before considering
    /// the attempt timed out and applying retry logic.
    /// Used by retrying append sessions.
    /// Default: 5000ms
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMilliseconds(5000);

    /// <summary>
    /// Maximum time to wait for connection establishment.
    /// This is a "fail fast" timeout that aborts slow connections early.
    /// Connection time counts toward RequestTimeout.
    /// Default: 5000ms
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromMilliseconds(5000);

    /// <summary>
    /// Creates a default retry configuration.
    /// </summary>
    public static RetryConfig Default => new();
}

/// <summary>
/// Per-request options that apply to all SDK operations.
/// </summary>
public sealed class S2RequestOptions
{
    /// <summary>
    /// Optional cancellation token to cancel the underlying HTTP request.
    /// Equivalent to AbortSignal in the TypeScript SDK.
    /// </summary>
    public CancellationToken CancellationToken { get; set; } = default;

    /// <summary>
    /// Creates request options with the specified cancellation token.
    /// </summary>
    public static S2RequestOptions WithCancellation(CancellationToken cancellationToken) =>
        new() { CancellationToken = cancellationToken };
}

/// <summary>
/// Configuration options for the S2 client.
/// </summary>
public sealed class S2Options
{
    /// <summary>
    /// S2 access token for authentication.
    /// Obtain from https://s2.dev/dashboard
    /// </summary>
    public required string AccessToken { get; set; }

    /// <summary>
    /// S2 region endpoint. Defaults to AWS.
    /// </summary>
    public S2Region Region { get; set; } = S2Region.AWS;

    /// <summary>
    /// HTTP request timeout. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Retry configuration for handling transient failures.
    /// Applies to management operations (basins, streams, tokens) and stream operations (read, append).
    /// </summary>
    public RetryConfig Retry { get; set; } = new();

    /// <summary>
    /// Maximum retry attempts for transient failures. Defaults to 3.
    /// </summary>
    [Obsolete("Use Retry.MaxAttempts instead")]
    public int MaxRetries
    {
        get => Retry.MaxAttempts;
        set => Retry.MaxAttempts = value;
    }

    /// <summary>
    /// Base delay between retries (exponential backoff). Defaults to 100ms.
    /// </summary>
    [Obsolete("Use Retry.MinDelay instead")]
    public TimeSpan RetryBaseDelay
    {
        get => Retry.MinDelay;
        set => Retry.MinDelay = value;
    }

    /// <summary>
    /// Custom HttpClient factory. If null, a default client is created.
    /// </summary>
    public Func<HttpClient>? HttpClientFactory { get; set; }

    /// <summary>
    /// Pre-configured HttpClient instance to use.
    /// Useful for Blazor WASM where HttpClient is injected via DI.
    /// The client will NOT be disposed by S2Client.
    /// </summary>
    public HttpClient? HttpClient { get; set; }

    /// <summary>
    /// Custom endpoints configuration.
    /// If null, defaults are used based on Region.
    /// </summary>
    public S2Endpoints? Endpoints { get; set; }

    internal string GetBaseUrl() => Endpoints?.AccountEndpoint ?? Region switch
    {
        S2Region.AWS => "https://aws.s2.dev",
        _ => throw new ArgumentOutOfRangeException(nameof(Region))
    };

    internal string GetAccountUrl() => $"{GetBaseUrl()}/v1";

    internal string GetBasinUrl(string basin) =>
        Endpoints?.GetBasinEndpoint(basin) ?? $"https://{basin}.b.aws.s2.dev";
}

/// <summary>
/// Custom endpoint configuration for the S2 environment.
/// </summary>
public sealed class S2Endpoints
{
    /// <summary>
    /// Account endpoint URL (e.g., "https://aws.s2.dev").
    /// </summary>
    public string? AccountEndpoint { get; set; }

    /// <summary>
    /// Basin endpoint URL template.
    /// Use {basin} as placeholder for the basin name.
    /// Example: "https://{basin}.b.aws.s2.dev"
    /// </summary>
    public string? BasinEndpointTemplate { get; set; }

    /// <summary>
    /// Gets the basin endpoint for a specific basin name.
    /// </summary>
    public string? GetBasinEndpoint(string basin) =>
        BasinEndpointTemplate?.Replace("{basin}", basin);
}

/// <summary>
/// S2 deployment regions.
/// </summary>
public enum S2Region
{
    /// <summary>
    /// AWS region (default).
    /// </summary>
    AWS
}

/// <summary>
/// Utility for parsing S2 configuration from environment variables.
/// </summary>
public static class S2Environment
{
    /// <summary>
    /// Environment variable name for S2 access token.
    /// </summary>
    public const string AccessTokenEnvVar = "S2_ACCESS_TOKEN";

    /// <summary>
    /// Environment variable name for S2 account endpoint.
    /// </summary>
    public const string AccountEndpointEnvVar = "S2_ACCOUNT_ENDPOINT";

    /// <summary>
    /// Environment variable name for S2 basin endpoint template.
    /// </summary>
    public const string BasinEndpointEnvVar = "S2_BASIN_ENDPOINT";

    /// <summary>
    /// Parses S2 configuration from environment variables.
    /// Environment variables:
    /// - S2_ACCESS_TOKEN: Access token for authentication
    /// - S2_ACCOUNT_ENDPOINT: Account endpoint URL
    /// - S2_BASIN_ENDPOINT: Basin endpoint URL template (use {basin} as placeholder)
    /// </summary>
    /// <returns>Partially configured S2Options from environment.</returns>
    public static S2EnvironmentConfig Parse()
    {
        var config = new S2EnvironmentConfig();

        var token = Environment.GetEnvironmentVariable(AccessTokenEnvVar);
        if (!string.IsNullOrEmpty(token))
        {
            config.AccessToken = token;
        }

        var accountEndpoint = Environment.GetEnvironmentVariable(AccountEndpointEnvVar);
        var basinEndpoint = Environment.GetEnvironmentVariable(BasinEndpointEnvVar);

        if (!string.IsNullOrEmpty(accountEndpoint) || !string.IsNullOrEmpty(basinEndpoint))
        {
            config.Endpoints = new S2Endpoints
            {
                AccountEndpoint = accountEndpoint,
                BasinEndpointTemplate = basinEndpoint
            };
        }

        return config;
    }

    /// <summary>
    /// Creates S2Options from environment variables.
    /// Throws if S2_ACCESS_TOKEN is not set.
    /// </summary>
    public static S2Options CreateOptions()
    {
        var config = Parse();
        if (string.IsNullOrEmpty(config.AccessToken))
        {
            throw new InvalidOperationException(
                $"Environment variable {AccessTokenEnvVar} is required but not set.");
        }

        return new S2Options
        {
            AccessToken = config.AccessToken,
            Endpoints = config.Endpoints
        };
    }
}

/// <summary>
/// Configuration parsed from environment variables.
/// </summary>
public sealed class S2EnvironmentConfig
{
    /// <summary>
    /// Access token from S2_ACCESS_TOKEN environment variable.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Endpoints configuration from environment variables.
    /// </summary>
    public S2Endpoints? Endpoints { get; set; }
}
