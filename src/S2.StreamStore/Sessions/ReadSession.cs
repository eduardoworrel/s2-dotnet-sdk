using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using S2.StreamStore.Models;

namespace S2.StreamStore.Sessions;

/// <summary>
/// A session for reading records from an S2 stream.
/// Supports streaming reads with IAsyncEnumerable.
/// </summary>
public sealed class ReadSession : IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly HttpClient _httpClient;
    private readonly ReadSessionOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    internal ReadSession(Stream stream, HttpClient httpClient, ReadSessionOptions options)
    {
        _stream = stream;
        _httpClient = httpClient;
        _options = options;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Read all records from the stream as an async enumerable.
    /// </summary>
    public async IAsyncEnumerable<Record> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var linkedCt = _cts.Token;

        var url = $"{_stream.RecordsUrl}?{_options.Start.ToQueryParam()}&clamp=true";
        long recordsRead = 0;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            linkedCt);

        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(linkedCt);
        using var reader = new StreamReader(responseStream, Encoding.UTF8);

        var eventData = new StringBuilder();

        while (!reader.EndOfStream && !linkedCt.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(linkedCt);

            if (line == null)
                break;

            // SSE format: data lines followed by empty line
            if (line.StartsWith("data:"))
            {
                eventData.Append(line.AsSpan(5).Trim());
            }
            else if (string.IsNullOrEmpty(line) && eventData.Length > 0)
            {
                // End of event - parse and yield
                var data = eventData.ToString();
                eventData.Clear();

                if (string.IsNullOrWhiteSpace(data))
                    continue;

                Record? record;
                try
                {
                    record = ParseRecord(data);
                }
                catch
                {
                    // Skip malformed records
                    continue;
                }

                if (record != null)
                {
                    yield return record;
                    recordsRead++;

                    if (_options.MaxRecords.HasValue && recordsRead >= _options.MaxRecords.Value)
                        yield break;
                }
            }
        }
    }

    private Record? ParseRecord(string json)
    {
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // S2 returns records in an array, or single record
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            return ParseRecordElement(root[0]);
        }

        if (root.TryGetProperty("body", out _))
        {
            return ParseRecordElement(root);
        }

        return null;
    }

    private Record ParseRecordElement(JsonElement element)
    {
        var bodyBase64 = element.GetProperty("body").GetString()
            ?? throw new InvalidOperationException("Record missing body");

        byte[] body;
        try
        {
            body = Convert.FromBase64String(bodyBase64);
        }
        catch
        {
            // Not base64 - treat as raw string
            body = Encoding.UTF8.GetBytes(bodyBase64);
        }

        var seqNum = element.TryGetProperty("seq", out var seqProp)
            ? seqProp.GetInt64()
            : element.TryGetProperty("sequence_number", out var seqProp2)
                ? seqProp2.GetInt64()
                : 0;

        DateTimeOffset? timestamp = null;
        if (element.TryGetProperty("timestamp", out var tsProp))
        {
            if (tsProp.ValueKind == JsonValueKind.Number)
            {
                timestamp = DateTimeOffset.FromUnixTimeMilliseconds(tsProp.GetInt64());
            }
            else if (tsProp.ValueKind == JsonValueKind.String)
            {
                timestamp = DateTimeOffset.Parse(tsProp.GetString()!);
            }
        }

        return new Record
        {
            SequenceNumber = seqNum,
            Body = body,
            Timestamp = timestamp
        };
    }

    /// <summary>
    /// Stop the read session.
    /// </summary>
    public void Cancel()
    {
        _cts?.Cancel();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _cts?.Cancel();
        _cts?.Dispose();

        await Task.CompletedTask;
    }
}
