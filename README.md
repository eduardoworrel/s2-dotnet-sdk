# S2 .NET SDK

.NET SDK for [S2.dev](https://s2.dev) - the streaming data platform.

[![NuGet](https://img.shields.io/nuget/v/S2.StreamStore.svg)](https://www.nuget.org/packages/S2.StreamStore)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

## Installation

```bash
dotnet add package S2.StreamStore
```

## Quick Start

```csharp
using S2.StreamStore;

// Create client
var s2 = new S2Client(new S2Options
{
    AccessToken = "s2_your_token_here"
});

// Get stream reference
var basin = s2.Basin("my-basin");
var stream = basin.Stream("my-stream");

// Create stream if it doesn't exist
await stream.CreateAsync();

// Append a record
var receipt = await stream.AppendAsync(new { message = "Hello, S2!", timestamp = DateTime.UtcNow });
Console.WriteLine($"Appended at sequence: {receipt.SequenceNumber}");

// Read records
await foreach (var record in stream.ReadAsync())
{
    var data = record.Deserialize<MyEvent>();
    Console.WriteLine($"[{record.SequenceNumber}] {data.Message}");
}
```

## Features

### Append Records

```csharp
// Single append
var receipt = await stream.AppendAsync(new { userId = 123, action = "click" });

// Batch append
var response = await stream.AppendBatchAsync(new[]
{
    new { tick = 1, data = "..." },
    new { tick = 2, data = "..." },
    new { tick = 3, data = "..." }
});
Console.WriteLine($"Batch appended: {response.StartSequenceNumber} - {response.EndSequenceNumber}");
```

### Raw Records with Fencing and Trim

```csharp
using S2.StreamStore.Models;

// Create raw string record
var record = AppendRecord.String("Hello, world!");

// Create record with headers
var recordWithHeaders = AppendRecord.String(
    body: "event data",
    headers: [("event-type", "user-created"), ("version", "1")]
);

// Set a fencing token (subsequent appends must provide this token)
await stream.SetFenceAsync("my-fence-token");

// Append with fencing token
await stream.AppendRecordAsync(
    AppendRecord.String("protected data"),
    fencingToken: "my-fence-token"
);

// Trim records before sequence number (marks for deletion)
await stream.TrimAsync(seqNum: 1000);

// Optimistic concurrency with matchSeqNum
await stream.AppendRecordsAsync(
    records: [AppendRecord.String("data")],
    matchSeqNum: 1234  // Fails if current seq != 1234
);
```

### High-Throughput Append Session

For maximum throughput, use `AppendSession` with automatic batching and pipelining:

```csharp
await using var session = stream.OpenAppendSession(new AppendSessionOptions
{
    BatchSize = 100,              // Records per batch
    BatchTimeout = TimeSpan.FromMilliseconds(50),  // Max wait before flush
    MaxConcurrentBatches = 4      // Pipelining
});

// Fire-and-forget appends (buffered internally)
for (int i = 0; i < 100_000; i++)
{
    await session.AppendAsync(new GameEvent { Tick = i, Players = [...] });
}

// Wait for all records to be sent
await session.FlushAsync();

Console.WriteLine($"Sent {session.TotalSent} records");
```

### Producer with Per-Record Acknowledgments

For precise control over durability acknowledgments:

```csharp
await using var producer = new Producer(stream, new ProducerOptions
{
    LingerDuration = TimeSpan.FromMilliseconds(5),
    MaxBatchRecords = 100,
    FencingToken = "my-token"
});

// Submit returns immediately when record is accepted
var ticket = await producer.SubmitAsync(AppendRecord.String("important data"));

// Wait for durability confirmation
var ack = await ticket.AckAsync();
Console.WriteLine($"Record durable at sequence: {ack.SeqNum}");

await producer.CloseAsync();
```

### Check Tail Position

```csharp
var tail = await stream.CheckTailAsync();
Console.WriteLine($"Next seq: {tail.Tail?.SeqNum}, Timestamp: {tail.Tail?.TimestampDate}");
```

### Read Records

```csharp
// Read from tail (latest)
await foreach (var record in stream.ReadAsync())
{
    var data = record.Deserialize<MyEvent>();
    ProcessEvent(data);
}

// Read from beginning
await foreach (var record in stream.ReadAsync(new ReadSessionOptions
{
    Start = ReadStart.FromBeginning()
}))
{
    // ...
}

// Read from specific sequence number
await foreach (var record in stream.ReadAsync(new ReadSessionOptions
{
    Start = ReadStart.FromSequence(12345)
}))
{
    // ...
}
```

### Basin Management

```csharp
// List all basins
var basinsResponse = await s2.Basins.ListAsync();
foreach (var b in basinsResponse.Basins)
{
    Console.WriteLine($"Basin: {b.Name}, State: {b.State}");
}

// Create a basin
await s2.Basins.CreateAsync(new CreateBasinInput
{
    Basin = "new-basin",
    Config = new BasinConfig { CreateStreamOnAppend = true }
});

// Get basin config
var config = await s2.Basins.GetConfigAsync("my-basin");

// Delete basin
await s2.Basins.DeleteAsync("old-basin");
```

### Stream Management

```csharp
// List streams in a basin
var streamsResponse = await basin.Streams.ListAsync(prefix: "game-");
foreach (var s in streamsResponse.Streams)
{
    Console.WriteLine($"Stream: {s.Name}");
}

// Get stream config
var streamConfig = await basin.Streams.GetConfigAsync("my-stream");

// Reconfigure stream
await basin.Streams.ReconfigureAsync("my-stream", new StreamConfig
{
    StorageClass = "standard"
});
```

### Access Tokens

```csharp
// List access tokens
var tokensResponse = await s2.AccessTokens.ListAsync();

// Issue a scoped token
var response = await s2.AccessTokens.IssueAsync(new IssueAccessTokenInput
{
    Id = "client-token-1",
    Scope = new AccessTokenScope
    {
        Basins = new ResourceSet { Exact = "my-basin" },
        Streams = new ResourceSet { Prefix = "user-" },
        Ops = ["read", "append"]
    },
    ExpiresAt = DateTime.UtcNow.AddHours(24).ToString("o")
});
Console.WriteLine($"Token: {response.AccessToken}");

// Revoke a token
await s2.AccessTokens.RevokeAsync("client-token-1");
```

### Metrics

```csharp
// Account-level metrics
var accountMetrics = await s2.Metrics.GetAccountMetricsAsync(
    metricSet: "usage",
    start: DateTimeOffset.UtcNow.AddDays(-7),
    end: DateTimeOffset.UtcNow,
    interval: "day"
);

// Basin-level metrics
var basinMetrics = await s2.Metrics.GetBasinMetricsAsync(
    basinName: "my-basin",
    metricSet: "info"
);

// Stream-level metrics
var streamMetrics = await s2.Metrics.GetStreamMetricsAsync(
    basinName: "my-basin",
    streamName: "my-stream",
    metricSet: "usage"
);
```

## Dependency Injection

```csharp
// In Program.cs or Startup.cs
builder.Services.AddSingleton(new S2Client(new S2Options
{
    AccessToken = builder.Configuration["S2:Token"]!
}));

// In your service
public class GameService(S2Client s2)
{
    private readonly Stream _stream = s2.Basin("game").Stream("events");

    public async Task PublishEvent(GameEvent evt)
    {
        await _stream.AppendAsync(evt);
    }
}
```

## Blazor WebAssembly

The SDK is compatible with Blazor WASM. Pass the injected `HttpClient`:

```csharp
// Program.cs
builder.Services.AddScoped(sp => new S2Client(new S2Options
{
    AccessToken = "s2_...",
    HttpClient = sp.GetRequiredService<HttpClient>()  // Use Blazor's HttpClient
}));
```

```razor
@* In your component *@
@inject S2Client S2

@code {
    private List<string> messages = new();

    protected override async Task OnInitializedAsync()
    {
        var stream = S2.Basin("my-basin").Stream("chat");

        await foreach (var record in stream.ReadAsync())
        {
            messages.Add(record.GetBodyAsString());
            StateHasChanged();
        }
    }

    private async Task SendMessage(string text)
    {
        var stream = S2.Basin("my-basin").Stream("chat");
        await stream.AppendAsync(new { text, timestamp = DateTime.UtcNow });
    }
}
```

## Configuration Options

```csharp
var s2 = new S2Client(new S2Options
{
    AccessToken = "s2_...",
    Region = S2Region.AWS,           // Default region
    Timeout = TimeSpan.FromSeconds(30),
    MaxRetries = 3,
    RetryBaseDelay = TimeSpan.FromMilliseconds(100)
});
```

## Requirements

- .NET 10.0 or later
- S2 account and access token from [s2.dev](https://s2.dev)

## Links

- [S2 Documentation](https://s2.dev/docs)
- [S2 Pricing](https://s2.dev/pricing)
- [TypeScript SDK](https://github.com/s2-streamstore/s2-sdk-typescript)

## License

MIT License - see [LICENSE](LICENSE) for details.
