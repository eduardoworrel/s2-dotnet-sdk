using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using S2.StreamStore.Exceptions;

namespace S2.StreamStore.Http;

/// <summary>
/// HTTP client for S2 API with retry logic and error handling.
/// </summary>
internal sealed class S2HttpClient : IDisposable
{
    private readonly HttpClient _client;
    private readonly S2Options _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    public S2HttpClient(S2Options options)
    {
        _options = options;

        _client = options.HttpClientFactory?.Invoke() ?? CreateDefaultClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", options.AccessToken);
        _client.Timeout = options.Timeout;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };
    }

    private static HttpClient CreateDefaultClient()
    {
        var handler = new SocketsHttpHandler
        {
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13,
                // Skip ALPN negotiation issues
                ApplicationProtocols = null
            }
        };
        return new HttpClient(handler)
        {
            // Force HTTP/1.1 to avoid HTTP/2 ALPN issues
            DefaultRequestVersion = System.Net.HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };
    }

    /// <summary>
    /// Send a GET request with retry logic.
    /// </summary>
    public async Task<T> GetAsync<T>(string url, CancellationToken ct = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var response = await _client.GetAsync(url, ct);
            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<T>(_jsonOptions, ct)
                ?? throw new S2Exception("Empty response body");
        }, ct);
    }

    /// <summary>
    /// Send a POST request with JSON body and retry logic.
    /// </summary>
    public async Task<TResponse> PostAsync<TRequest, TResponse>(
        string url,
        TRequest body,
        CancellationToken ct = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var json = JsonSerializer.Serialize(body, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(url, content, ct);
            await EnsureSuccessAsync(response);

            return await response.Content.ReadFromJsonAsync<TResponse>(_jsonOptions, ct)
                ?? throw new S2Exception("Empty response body");
        }, ct);
    }

    /// <summary>
    /// Send a POST request with JSON body, no response body expected.
    /// </summary>
    public async Task PostAsync<TRequest>(string url, TRequest body, CancellationToken ct = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            var json = JsonSerializer.Serialize(body, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _client.PostAsync(url, content, ct);
            await EnsureSuccessAsync(response);
            return true;
        }, ct);
    }

    /// <summary>
    /// Send a PUT request (for stream/basin creation).
    /// </summary>
    public async Task<bool> PutAsync(string url, CancellationToken ct = default)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var content = new StringContent("{}", Encoding.UTF8, "application/json");
            var response = await _client.PutAsync(url, content, ct);

            // 409 Conflict means already exists - not an error for create operations
            if (response.StatusCode == HttpStatusCode.Conflict)
                return false;

            await EnsureSuccessAsync(response);
            return true;
        }, ct);
    }

    /// <summary>
    /// Send a DELETE request.
    /// </summary>
    public async Task DeleteAsync(string url, CancellationToken ct = default)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            var response = await _client.DeleteAsync(url, ct);
            await EnsureSuccessAsync(response);
            return true;
        }, ct);
    }

    /// <summary>
    /// Get underlying HttpClient for streaming operations.
    /// </summary>
    public HttpClient GetHttpClient() => _client;

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        CancellationToken ct)
    {
        var attempt = 0;
        var delay = _options.RetryBaseDelay;

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (S2Exception ex) when (ShouldRetry(ex) && attempt < _options.MaxRetries)
            {
                attempt++;
                await Task.Delay(delay, ct);
                delay *= 2; // Exponential backoff
            }
            catch (HttpRequestException ex) when (attempt < _options.MaxRetries)
            {
                attempt++;
                await Task.Delay(delay, ct);
                delay *= 2;
            }
        }
    }

    private static bool ShouldRetry(S2Exception ex)
    {
        return ex.StatusCode is
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout or
            HttpStatusCode.BadGateway;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var content = await response.Content.ReadAsStringAsync();

        throw response.StatusCode switch
        {
            HttpStatusCode.NotFound => new S2Exception($"Not found: {content}", response.StatusCode),
            HttpStatusCode.Unauthorized => new AuthenticationException(),
            HttpStatusCode.Forbidden => new AuthenticationException("Access denied"),
            HttpStatusCode.TooManyRequests => new RateLimitedException(
                response.Headers.RetryAfter?.Delta),
            HttpStatusCode.Conflict => new AlreadyExistsException(content),
            _ => new S2Exception($"HTTP {(int)response.StatusCode}: {content}", response.StatusCode)
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _client.Dispose();
    }
}
