using S2.StreamStore.Http;
using S2.StreamStore.Models;

namespace S2.StreamStore;

/// <summary>
/// Helper for querying account, basin, and stream level metrics.
/// </summary>
public sealed class S2Metrics
{
    private readonly S2HttpClient _httpClient;
    private readonly string _accountUrl;

    internal S2Metrics(S2HttpClient httpClient, string accountUrl)
    {
        _httpClient = httpClient;
        _accountUrl = accountUrl;
    }

    /// <summary>
    /// Get account-level metrics.
    /// </summary>
    /// <param name="metricSet">Metric set to return (e.g., "info", "usage").</param>
    /// <param name="start">Optional start timestamp.</param>
    /// <param name="end">Optional end timestamp.</param>
    /// <param name="interval">Optional aggregation interval.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<MetricSetResponse> GetAccountMetricsAsync(
        string metricSet,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        string? interval = null,
        CancellationToken ct = default)
    {
        var query = BuildMetricsQuery(metricSet, start, end, interval);
        var url = $"{_accountUrl}/metrics?{query}";
        return await _httpClient.GetAsync<MetricSetResponse>(url, ct);
    }

    /// <summary>
    /// Get basin-level metrics.
    /// </summary>
    /// <param name="basinName">Basin name.</param>
    /// <param name="metricSet">Metric set to return.</param>
    /// <param name="start">Optional start timestamp.</param>
    /// <param name="end">Optional end timestamp.</param>
    /// <param name="interval">Optional aggregation interval.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<MetricSetResponse> GetBasinMetricsAsync(
        string basinName,
        string metricSet,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        string? interval = null,
        CancellationToken ct = default)
    {
        var query = BuildMetricsQuery(metricSet, start, end, interval);
        var url = $"{_accountUrl}/basins/{Uri.EscapeDataString(basinName)}/metrics?{query}";
        return await _httpClient.GetAsync<MetricSetResponse>(url, ct);
    }

    /// <summary>
    /// Get stream-level metrics.
    /// </summary>
    /// <param name="basinName">Basin name.</param>
    /// <param name="streamName">Stream name.</param>
    /// <param name="metricSet">Metric set to return.</param>
    /// <param name="start">Optional start timestamp.</param>
    /// <param name="end">Optional end timestamp.</param>
    /// <param name="interval">Optional aggregation interval.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<MetricSetResponse> GetStreamMetricsAsync(
        string basinName,
        string streamName,
        string metricSet,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null,
        string? interval = null,
        CancellationToken ct = default)
    {
        var query = BuildMetricsQuery(metricSet, start, end, interval);
        var url = $"{_accountUrl}/basins/{Uri.EscapeDataString(basinName)}/streams/{Uri.EscapeDataString(streamName)}/metrics?{query}";
        return await _httpClient.GetAsync<MetricSetResponse>(url, ct);
    }

    private static string BuildMetricsQuery(
        string metricSet,
        DateTimeOffset? start,
        DateTimeOffset? end,
        string? interval)
    {
        var parts = new List<string> { $"set={Uri.EscapeDataString(metricSet)}" };

        if (start.HasValue)
            parts.Add($"start={start.Value.ToUnixTimeSeconds()}");
        if (end.HasValue)
            parts.Add($"end={end.Value.ToUnixTimeSeconds()}");
        if (!string.IsNullOrEmpty(interval))
            parts.Add($"interval={Uri.EscapeDataString(interval)}");

        return string.Join("&", parts);
    }
}
