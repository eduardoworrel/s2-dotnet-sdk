using System.Text.Json.Serialization;

namespace S2.StreamStore.Models;

/// <summary>
/// A scalar metric with a single value.
/// </summary>
public sealed class ScalarMetric
{
    /// <summary>
    /// Metric name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Unit of the metric.
    /// </summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; init; }

    /// <summary>
    /// Metric value.
    /// </summary>
    [JsonPropertyName("value")]
    public double Value { get; init; }
}

/// <summary>
/// An accumulation metric with timeseries values.
/// </summary>
public sealed class AccumulationMetric
{
    /// <summary>
    /// The interval at which data points are accumulated.
    /// </summary>
    [JsonPropertyName("interval")]
    public string? Interval { get; init; }

    /// <summary>
    /// Timeseries name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Unit of the metric.
    /// </summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; init; }

    /// <summary>
    /// Timeseries values as [timestamp, value] pairs.
    /// </summary>
    [JsonPropertyName("values")]
    public List<List<double>> Values { get; init; } = [];
}

/// <summary>
/// A gauge metric with instantaneous values.
/// </summary>
public sealed class GaugeMetric
{
    /// <summary>
    /// Timeseries name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Unit of the metric.
    /// </summary>
    [JsonPropertyName("unit")]
    public string? Unit { get; init; }

    /// <summary>
    /// Timeseries values as [timestamp, value] pairs.
    /// </summary>
    [JsonPropertyName("values")]
    public List<List<double>> Values { get; init; } = [];
}

/// <summary>
/// A label metric with string values.
/// </summary>
public sealed class LabelMetric
{
    /// <summary>
    /// Label name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>
    /// Label values.
    /// </summary>
    [JsonPropertyName("values")]
    public List<string> Values { get; init; } = [];
}

/// <summary>
/// A metric in a metric set response.
/// </summary>
public sealed class Metric
{
    /// <summary>
    /// Scalar metric (if present).
    /// </summary>
    [JsonPropertyName("scalar")]
    public ScalarMetric? Scalar { get; init; }

    /// <summary>
    /// Accumulation metric (if present).
    /// </summary>
    [JsonPropertyName("accumulation")]
    public AccumulationMetric? Accumulation { get; init; }

    /// <summary>
    /// Gauge metric (if present).
    /// </summary>
    [JsonPropertyName("gauge")]
    public GaugeMetric? Gauge { get; init; }

    /// <summary>
    /// Label metric (if present).
    /// </summary>
    [JsonPropertyName("label")]
    public LabelMetric? Label { get; init; }
}

/// <summary>
/// Response from a metrics query.
/// </summary>
public sealed class MetricSetResponse
{
    /// <summary>
    /// Metrics comprising the set.
    /// </summary>
    [JsonPropertyName("values")]
    public List<Metric> Values { get; init; } = [];
}

/// <summary>
/// Account metric set types.
/// </summary>
public static class AccountMetricSet
{
    public const string Info = "info";
    public const string Usage = "usage";
}

/// <summary>
/// Basin metric set types.
/// </summary>
public static class BasinMetricSet
{
    public const string Info = "info";
    public const string Usage = "usage";
}

/// <summary>
/// Stream metric set types.
/// </summary>
public static class StreamMetricSet
{
    public const string Info = "info";
    public const string Usage = "usage";
}

/// <summary>
/// Timeseries interval for metrics aggregation.
/// </summary>
public static class TimeseriesInterval
{
    public const string Minute = "minute";
    public const string Hour = "hour";
    public const string Day = "day";
}
