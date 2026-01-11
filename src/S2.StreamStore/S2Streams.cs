using S2.StreamStore.Http;
using S2.StreamStore.Models;

namespace S2.StreamStore;

/// <summary>
/// Basin-scoped helper for listing and configuring streams.
/// </summary>
public sealed class S2Streams
{
    private readonly S2HttpClient _httpClient;
    private readonly string _baseUrl;

    internal S2Streams(S2HttpClient httpClient, string baseUrl)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl;
    }

    private string StreamsUrl => $"{_baseUrl}/v1/streams";

    /// <summary>
    /// List streams in the basin.
    /// </summary>
    /// <param name="prefix">Filter to streams whose names start with this prefix.</param>
    /// <param name="startAfter">Name to start after (for pagination).</param>
    /// <param name="limit">Max results (up to 1000).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<ListStreamsResponse> ListAsync(
        string? prefix = null,
        string? startAfter = null,
        int? limit = null,
        CancellationToken ct = default)
    {
        var query = BuildQuery(prefix, startAfter, limit);
        var url = string.IsNullOrEmpty(query) ? StreamsUrl : $"{StreamsUrl}?{query}";
        return await _httpClient.GetAsync<ListStreamsResponse>(url, ct);
    }

    /// <summary>
    /// List all streams in the basin with automatic pagination.
    /// </summary>
    public async IAsyncEnumerable<StreamInfo> ListAllAsync(
        string? prefix = null,
        bool includeDeleted = false,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        string? startAfter = null;
        bool hasMore = true;

        while (hasMore && !ct.IsCancellationRequested)
        {
            var response = await ListAsync(prefix, startAfter, 1000, ct);

            foreach (var stream in response.Streams)
            {
                if (includeDeleted || stream.DeletedAt == null)
                {
                    yield return stream;
                    startAfter = stream.Name;
                }
            }

            hasMore = response.HasMore;
        }
    }

    /// <summary>
    /// Create a stream.
    /// </summary>
    /// <param name="input">Stream creation input.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<StreamConfig> CreateAsync(CreateStreamInput input, CancellationToken ct = default)
    {
        return await _httpClient.PostAsync<CreateStreamInput, StreamConfig>(StreamsUrl, input, ct);
    }

    /// <summary>
    /// Create a stream with just a name.
    /// </summary>
    public async Task<StreamConfig> CreateAsync(string streamName, CancellationToken ct = default)
    {
        return await CreateAsync(new CreateStreamInput { Stream = streamName }, ct);
    }

    /// <summary>
    /// Get stream configuration.
    /// </summary>
    /// <param name="streamName">Stream name.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<StreamConfig> GetConfigAsync(string streamName, CancellationToken ct = default)
    {
        return await _httpClient.GetAsync<StreamConfig>($"{StreamsUrl}/{streamName}", ct);
    }

    /// <summary>
    /// Delete a stream.
    /// </summary>
    /// <param name="streamName">Stream name.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task DeleteAsync(string streamName, CancellationToken ct = default)
    {
        await _httpClient.DeleteAsync($"{StreamsUrl}/{streamName}", ct);
    }

    /// <summary>
    /// Reconfigure a stream.
    /// </summary>
    /// <param name="streamName">Stream name.</param>
    /// <param name="config">New configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<StreamConfig> ReconfigureAsync(
        string streamName,
        StreamConfig config,
        CancellationToken ct = default)
    {
        return await _httpClient.PostAsync<StreamConfig, StreamConfig>(
            $"{StreamsUrl}/{streamName}", config, ct);
    }

    private static string BuildQuery(string? prefix, string? startAfter, int? limit)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(prefix))
            parts.Add($"prefix={Uri.EscapeDataString(prefix)}");
        if (!string.IsNullOrEmpty(startAfter))
            parts.Add($"start_after={Uri.EscapeDataString(startAfter)}");
        if (limit.HasValue)
            parts.Add($"limit={limit.Value}");
        return string.Join("&", parts);
    }
}
