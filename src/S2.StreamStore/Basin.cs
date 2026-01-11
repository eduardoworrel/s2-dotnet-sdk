using S2.StreamStore.Http;
using S2.StreamStore.Models;

namespace S2.StreamStore;

/// <summary>
/// Represents an S2 basin (a logical grouping of streams).
/// </summary>
public sealed class Basin
{
    private readonly string _name;
    private readonly S2Options _options;
    private readonly S2HttpClient _httpClient;

    internal Basin(string name, S2Options options, S2HttpClient httpClient)
    {
        _name = name;
        _options = options;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Basin name.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Get the basin-specific API URL.
    /// </summary>
    internal string BaseUrl => _options.GetBasinUrl(_name);

    /// <summary>
    /// Get a stream reference by name.
    /// Does not verify the stream exists - use GetInfoAsync() or CreateAsync() to interact.
    /// </summary>
    public Stream Stream(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Stream(name, this, _httpClient);
    }

    /// <summary>
    /// Get information about this basin.
    /// </summary>
    public async Task<BasinInfo> GetInfoAsync(CancellationToken ct = default)
    {
        var url = $"{_options.GetBaseUrl()}/v1/basins/{_name}";
        return await _httpClient.GetAsync<BasinInfo>(url, ct);
    }

    /// <summary>
    /// Create this basin if it doesn't exist.
    /// </summary>
    /// <returns>True if created, false if already existed.</returns>
    public async Task<bool> CreateAsync(CancellationToken ct = default)
    {
        var url = $"{_options.GetBaseUrl()}/v1/basins/{_name}";
        return await _httpClient.PutAsync(url, ct);
    }

    /// <summary>
    /// Delete this basin and all its streams.
    /// </summary>
    public async Task DeleteAsync(CancellationToken ct = default)
    {
        var url = $"{_options.GetBaseUrl()}/v1/basins/{_name}";
        await _httpClient.DeleteAsync(url, ct);
    }

    /// <summary>
    /// List all streams in this basin.
    /// </summary>
    public async Task<IReadOnlyList<StreamInfo>> ListStreamsAsync(CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/v1/streams";
        var response = await _httpClient.GetAsync<ListStreamsResponse>(url, ct);
        return response.Streams;
    }

    private sealed class ListStreamsResponse
    {
        public List<StreamInfo> Streams { get; set; } = [];
    }
}
