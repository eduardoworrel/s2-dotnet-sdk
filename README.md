# S2 .NET SDK

Official .NET SDK for [S2.dev](https://s2.dev) - the streaming data platform.

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

// Read limited number of records
await foreach (var record in stream.ReadAsync(new ReadSessionOptions
{
    Start = ReadStart.FromTail(100),
    MaxRecords = 100
}))
{
    // ...
}
```

### Read Session with Manual Control

```csharp
var session = stream.OpenReadSession(new ReadSessionOptions
{
    Start = ReadStart.FromTail(1)
});

try
{
    await foreach (var record in session.ReadAllAsync(cancellationToken))
    {
        // Process record
        if (shouldStop)
        {
            session.Cancel();
            break;
        }
    }
}
finally
{
    await session.DisposeAsync();
}
```

### Access Tokens (Scoped)

Create limited-scope tokens for clients:

```csharp
var token = await s2.CreateAccessTokenAsync(new AccessTokenRequest
{
    Basins = new ScopeFilter { Exact = "my-basin" },
    Streams = new ScopeFilter { Prefix = "user-" },
    Operations = ["read"],
    ExpiresAt = DateTime.UtcNow.AddHours(24)
});

// Give this token to frontend/clients
Console.WriteLine($"Client token: {token.AccessToken}");
```

### Basin and Stream Management

```csharp
// List basins
var basins = await s2.ListBasinsAsync();

// Create basin
var basin = s2.Basin("new-basin");
await basin.CreateAsync();

// List streams in basin
var streams = await basin.ListStreamsAsync();

// Get stream info
var info = await stream.GetInfoAsync();
Console.WriteLine($"Records: {info.RecordCount}, Size: {info.TotalSizeBytes} bytes");

// Delete stream
await stream.DeleteAsync();
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

## Error Handling

```csharp
try
{
    await stream.AppendAsync(data);
}
catch (StreamNotFoundException ex)
{
    Console.WriteLine($"Stream not found: {ex.StreamName}");
}
catch (RateLimitedException ex)
{
    Console.WriteLine($"Rate limited. Retry after: {ex.RetryAfter}");
    await Task.Delay(ex.RetryAfter ?? TimeSpan.FromSeconds(1));
}
catch (AuthenticationException)
{
    Console.WriteLine("Invalid or expired token");
}
catch (S2Exception ex)
{
    Console.WriteLine($"S2 error: {ex.Message} (HTTP {ex.StatusCode})");
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

- .NET 8.0 or later
- S2 account and access token from [s2.dev](https://s2.dev)

## Links

- [S2 Documentation](https://s2.dev/docs)
- [S2 Pricing](https://s2.dev/pricing)
- [TypeScript SDK](https://github.com/s2-streamstore/s2-sdk-typescript)

## License

MIT License - see [LICENSE](LICENSE) for details.

## Contributing

Contributions welcome! Please read our contributing guidelines before submitting PRs.
