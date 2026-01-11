using System.Text;
using System.Text.Json;
using S2.StreamStore.Http;
using S2.StreamStore.Models;
using S2.StreamStore.Sessions;

namespace S2.StreamStore;

/// <summary>
/// Represents an S2 stream for reading and writing records.
/// </summary>
public sealed class Stream
{
    private readonly string _name;
    private readonly Basin _basin;
    private readonly S2HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    internal Stream(string name, Basin basin, S2HttpClient httpClient)
    {
        _name = name;
        _basin = basin;
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Stream name.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Parent basin.
    /// </summary>
    public Basin Basin => _basin;

    /// <summary>
    /// Full URL for this stream's records endpoint.
    /// </summary>
    internal string RecordsUrl => $"{_basin.BaseUrl}/v1/streams/{_name}/records";

    /// <summary>
    /// Full URL for this stream.
    /// </summary>
    internal string StreamUrl => $"{_basin.BaseUrl}/v1/streams/{_name}";

    /// <summary>
    /// Get information about this stream.
    /// </summary>
    public async Task<StreamInfo> GetInfoAsync(CancellationToken ct = default)
    {
        return await _httpClient.GetAsync<StreamInfo>(StreamUrl, ct);
    }

    /// <summary>
    /// Check the tail position of the stream.
    /// Returns the next sequence number and timestamp to be assigned.
    /// </summary>
    public async Task<TailResponse> CheckTailAsync(CancellationToken ct = default)
    {
        return await _httpClient.GetAsync<TailResponse>($"{RecordsUrl}/tail", ct);
    }

    /// <summary>
    /// Create this stream if it doesn't exist.
    /// </summary>
    /// <returns>True if created, false if already existed.</returns>
    public async Task<bool> CreateAsync(CancellationToken ct = default)
    {
        return await _httpClient.PutAsync(StreamUrl, ct);
    }

    /// <summary>
    /// Delete this stream and all its records.
    /// </summary>
    public async Task DeleteAsync(CancellationToken ct = default)
    {
        await _httpClient.DeleteAsync(StreamUrl, ct);
    }

    /// <summary>
    /// Append a single record to the stream.
    /// </summary>
    /// <typeparam name="T">Type of the data to serialize.</typeparam>
    /// <param name="data">Data to append.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Receipt with sequence number.</returns>
    public async Task<AppendReceipt> AppendAsync<T>(T data, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        var base64Body = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var request = new AppendRequest
        {
            Records = [new AppendRequestRecord { Body = base64Body }]
        };

        var response = await _httpClient.PostAsync<AppendRequest, AppendResponse>(RecordsUrl, request, ct);

        return new AppendReceipt
        {
            SequenceNumber = response.StartSequenceNumber,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Append multiple records to the stream in a single batch.
    /// </summary>
    public async Task<AppendResponse> AppendBatchAsync<T>(
        IEnumerable<T> records,
        CancellationToken ct = default)
    {
        var request = new AppendRequest
        {
            Records = records.Select(r =>
            {
                var json = JsonSerializer.Serialize(r, _jsonOptions);
                return new AppendRequestRecord
                {
                    Body = Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
                };
            }).ToList()
        };

        return await _httpClient.PostAsync<AppendRequest, AppendResponse>(RecordsUrl, request, ct);
    }

    /// <summary>
    /// Append a single raw record to the stream.
    /// Use AppendRecord.String(), AppendRecord.Bytes(), AppendRecord.Fence(), or AppendRecord.Trim() to create records.
    /// </summary>
    /// <param name="record">The record to append.</param>
    /// <param name="fencingToken">Optional fencing token to enforce.</param>
    /// <param name="matchSeqNum">Optional sequence number to match for optimistic concurrency.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<AppendAck> AppendRecordAsync(
        AppendRecord record,
        string? fencingToken = null,
        long? matchSeqNum = null,
        CancellationToken ct = default)
    {
        var input = new AppendInput
        {
            Records = [record],
            FencingToken = fencingToken,
            MatchSeqNum = matchSeqNum
        };

        return await _httpClient.PostAsync<AppendInput, AppendAck>(RecordsUrl, input, ct);
    }

    /// <summary>
    /// Append multiple raw records to the stream with optional fencing and concurrency control.
    /// Use AppendRecord.String(), AppendRecord.Bytes(), AppendRecord.Fence(), or AppendRecord.Trim() to create records.
    /// </summary>
    /// <param name="records">The records to append.</param>
    /// <param name="fencingToken">Optional fencing token to enforce.</param>
    /// <param name="matchSeqNum">Optional sequence number to match for optimistic concurrency.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<AppendAck> AppendRecordsAsync(
        IEnumerable<AppendRecord> records,
        string? fencingToken = null,
        long? matchSeqNum = null,
        CancellationToken ct = default)
    {
        var input = new AppendInput
        {
            Records = records.ToList(),
            FencingToken = fencingToken,
            MatchSeqNum = matchSeqNum
        };

        return await _httpClient.PostAsync<AppendInput, AppendAck>(RecordsUrl, input, ct);
    }

    /// <summary>
    /// Set a fencing token on the stream.
    /// Subsequent appends must provide this token to succeed.
    /// </summary>
    /// <param name="fencingToken">The fencing token to set.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<AppendAck> SetFenceAsync(string fencingToken, CancellationToken ct = default)
    {
        return await AppendRecordAsync(AppendRecord.Fence(fencingToken), ct: ct);
    }

    /// <summary>
    /// Trim the stream, marking all records before the specified sequence number for deletion.
    /// </summary>
    /// <param name="seqNum">The sequence number to trim to (records before this will be deleted).</param>
    /// <param name="fencingToken">Optional fencing token to enforce.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<AppendAck> TrimAsync(long seqNum, string? fencingToken = null, CancellationToken ct = default)
    {
        return await AppendRecordAsync(AppendRecord.Trim(seqNum), fencingToken, ct: ct);
    }

    /// <summary>
    /// Open a read session to consume records from the stream.
    /// </summary>
    public ReadSession OpenReadSession(ReadSessionOptions? options = null)
    {
        return new ReadSession(this, _httpClient.GetHttpClient(), options ?? new ReadSessionOptions());
    }

    /// <summary>
    /// Open an append session for high-throughput writes with batching.
    /// </summary>
    public AppendSession OpenAppendSession(AppendSessionOptions? options = null)
    {
        return new AppendSession(this, _httpClient.GetHttpClient(), options ?? new AppendSessionOptions());
    }

    /// <summary>
    /// Read records from the stream as an async enumerable.
    /// Convenience method that creates a ReadSession internally.
    /// </summary>
    public IAsyncEnumerable<Record> ReadAsync(
        ReadSessionOptions? options = null,
        CancellationToken ct = default)
    {
        var session = OpenReadSession(options);
        return session.ReadAllAsync(ct);
    }

    /// <summary>
    /// Read records from the stream as strings.
    /// Bodies and headers are decoded as UTF-8 strings.
    /// </summary>
    public async IAsyncEnumerable<StringRecord> ReadStringsAsync(
        ReadSessionOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        options ??= new ReadSessionOptions();
        options.Format = ReadFormat.String;
        var session = OpenReadSession(options);
        await foreach (var record in session.ReadAllAsync(ct))
        {
            yield return StringRecord.FromRecord(record);
        }
    }

    /// <summary>
    /// Read records from the stream as bytes.
    /// Bodies and headers are kept as binary.
    /// </summary>
    public async IAsyncEnumerable<BytesRecord> ReadBytesAsync(
        ReadSessionOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        options ??= new ReadSessionOptions();
        options.Format = ReadFormat.Bytes;
        var session = OpenReadSession(options);
        await foreach (var record in session.ReadAllAsync(ct))
        {
            yield return BytesRecord.FromRecord(record);
        }
    }

    private sealed class AppendRequest
    {
        public List<AppendRequestRecord> Records { get; set; } = [];
    }

    private sealed class AppendRequestRecord
    {
        public required string Body { get; set; }
    }
}
