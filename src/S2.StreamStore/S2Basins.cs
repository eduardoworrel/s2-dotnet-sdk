using S2.StreamStore.Http;
using S2.StreamStore.Models;

namespace S2.StreamStore;

/// <summary>
/// Account-scoped helper for listing, creating, deleting, and reconfiguring basins.
/// </summary>
public sealed class S2Basins
{
    private readonly S2HttpClient _httpClient;
    private readonly string _accountUrl;

    internal S2Basins(S2HttpClient httpClient, string accountUrl)
    {
        _httpClient = httpClient;
        _accountUrl = accountUrl;
    }

    private string BasinsUrl => $"{_accountUrl}/basins";

    /// <summary>
    /// List basins.
    /// </summary>
    /// <param name="prefix">Filter to basins whose names start with this prefix.</param>
    /// <param name="startAfter">Name to start after (for pagination).</param>
    /// <param name="limit">Max results (up to 1000).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<ListBasinsResponse> ListAsync(
        string? prefix = null,
        string? startAfter = null,
        int? limit = null,
        CancellationToken ct = default)
    {
        var query = BuildQuery(prefix, startAfter, limit);
        var url = string.IsNullOrEmpty(query) ? BasinsUrl : $"{BasinsUrl}?{query}";
        return await _httpClient.GetAsync<ListBasinsResponse>(url, ct);
    }

    /// <summary>
    /// List all basins with automatic pagination.
    /// </summary>
    public async IAsyncEnumerable<BasinInfo> ListAllAsync(
        string? prefix = null,
        bool includeDeleted = false,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        string? startAfter = null;
        bool hasMore = true;

        while (hasMore && !ct.IsCancellationRequested)
        {
            var response = await ListAsync(prefix, startAfter, 1000, ct);

            foreach (var basin in response.Basins)
            {
                if (includeDeleted || basin.State != "deleting")
                {
                    yield return basin;
                    startAfter = basin.Name;
                }
            }

            hasMore = response.HasMore;
        }
    }

    /// <summary>
    /// Create a basin.
    /// </summary>
    /// <param name="input">Basin creation input.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<BasinInfo> CreateAsync(CreateBasinInput input, CancellationToken ct = default)
    {
        return await _httpClient.PostAsync<CreateBasinInput, BasinInfo>(BasinsUrl, input, ct);
    }

    /// <summary>
    /// Create a basin with just a name.
    /// </summary>
    public async Task<BasinInfo> CreateAsync(string basinName, CancellationToken ct = default)
    {
        return await CreateAsync(new CreateBasinInput { Basin = basinName }, ct);
    }

    /// <summary>
    /// Get basin configuration.
    /// </summary>
    /// <param name="basinName">Basin name.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<BasinConfig> GetConfigAsync(string basinName, CancellationToken ct = default)
    {
        return await _httpClient.GetAsync<BasinConfig>($"{BasinsUrl}/{basinName}", ct);
    }

    /// <summary>
    /// Delete a basin.
    /// </summary>
    /// <param name="basinName">Basin name.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task DeleteAsync(string basinName, CancellationToken ct = default)
    {
        await _httpClient.DeleteAsync($"{BasinsUrl}/{basinName}", ct);
    }

    /// <summary>
    /// Reconfigure a basin.
    /// </summary>
    /// <param name="basinName">Basin name.</param>
    /// <param name="config">New configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<BasinConfig> ReconfigureAsync(
        string basinName,
        BasinConfig config,
        CancellationToken ct = default)
    {
        return await _httpClient.PostAsync<BasinConfig, BasinConfig>(
            $"{BasinsUrl}/{basinName}", config, ct);
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
