namespace S2.StreamStore;

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
    /// Maximum retry attempts for transient failures. Defaults to 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Base delay between retries (exponential backoff). Defaults to 100ms.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Custom HttpClient factory. If null, a default client is created.
    /// </summary>
    public Func<HttpClient>? HttpClientFactory { get; set; }

    internal string GetBaseUrl() => Region switch
    {
        S2Region.AWS => "https://aws.s2.dev",
        _ => throw new ArgumentOutOfRangeException(nameof(Region))
    };

    internal string GetBasinUrl(string basin) => $"https://{basin}.b.aws.s2.dev";
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
