using System.Net.Http.Headers;
using S2.StreamStore;
using S2.StreamStore.Sessions;

Console.WriteLine("=== S2 .NET SDK Integration Tests ===\n");

// Test raw HttpClient against different servers
Console.WriteLine("--- Testing HttpClient connectivity ---\n");

using var testClient = new HttpClient();

// Test 1: GitWar Production (HTTPS)
Console.Write("1. GitWar Production (HTTPS)... ");
try
{
    var response = await testClient.GetAsync("https://gitwar.eduardoworrel.com/api/health");
    Console.WriteLine($"OK ({response.StatusCode})");
}
catch (Exception ex)
{
    Console.WriteLine($"FAILED: {ex.Message}");
}

// Test 2: Google (basic HTTPS test)
Console.Write("2. Google HTTPS... ");
try
{
    var response = await testClient.GetAsync("https://www.google.com");
    Console.WriteLine($"OK ({response.StatusCode})");
}
catch (Exception ex)
{
    Console.WriteLine($"FAILED: {ex.Message}");
}

// Test 3: S2.dev main site
Console.Write("3. S2.dev main site... ");
try
{
    var response = await testClient.GetAsync("https://s2.dev");
    Console.WriteLine($"OK ({response.StatusCode})");
}
catch (Exception ex)
{
    Console.WriteLine($"FAILED: {ex.Message}");
}

// Test 4: S2 basin endpoint (default)
Console.Write("4. S2 basin endpoint (default)... ");
try
{
    var response = await testClient.GetAsync("https://dotnet-sdk.b.aws.s2.dev/v1/streams");
    Console.WriteLine($"OK ({response.StatusCode})");
}
catch (Exception ex)
{
    Console.WriteLine($"FAILED: {ex.GetType().Name} - {ex.InnerException?.InnerException?.Message ?? ex.Message}");
}

// Test 5: S2 basin endpoint (HTTP/1.1 forced)
Console.Write("5. S2 basin endpoint (HTTP/1.1)... ");
try
{
    using var handler = new SocketsHttpHandler();
    using var http11Client = new HttpClient(handler)
    {
        DefaultRequestVersion = System.Net.HttpVersion.Version11,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
    };
    var response = await http11Client.GetAsync("https://dotnet-sdk.b.aws.s2.dev/v1/streams");
    Console.WriteLine($"OK ({response.StatusCode})");
}
catch (Exception ex)
{
    Console.WriteLine($"FAILED: {ex.GetType().Name} - {ex.InnerException?.InnerException?.Message ?? ex.Message}");
}

Console.WriteLine("\n--- S2 SDK Tests ---\n");

// Get token from environment or args
var token = Environment.GetEnvironmentVariable("S2_TOKEN")
    ?? (args.Length > 0 ? args[0] : null);

if (string.IsNullOrEmpty(token))
{
    Console.WriteLine("Usage: dotnet run -- <S2_TOKEN>");
    Console.WriteLine("   or: S2_TOKEN=<token> dotnet run");
    Console.WriteLine("\nGet your token from https://s2.dev/dashboard");
    return 1;
}

var basin = Environment.GetEnvironmentVariable("S2_BASIN") ?? "gitworld";
var testStreamName = $"sdk-test-{Guid.NewGuid():N}";

Console.WriteLine($"Token: {token[..20]}...");
Console.WriteLine($"Basin: {basin}");
Console.WriteLine($"Test Stream: {testStreamName}\n");

try
{
    // Create client
    var s2 = new S2Client(new S2Options
    {
        AccessToken = token,
        Timeout = TimeSpan.FromSeconds(30)
    });

    var basinRef = s2.Basin(basin);
    var stream = basinRef.Stream(testStreamName);

    // Test 1: Create Stream
    Console.Write("1. Creating stream... ");
    var created = await stream.CreateAsync();
    Console.WriteLine(created ? "OK (created)" : "OK (already exists)");

    // Test 2: Get Stream Info
    Console.Write("2. Getting stream info... ");
    var info = await stream.GetInfoAsync();
    Console.WriteLine($"OK (storage: {info.StorageClass})");

    // Test 2b: Check Tail
    Console.Write("2b. Checking tail... ");
    var tail = await stream.CheckTailAsync();
    Console.WriteLine($"OK (seqNum: {tail.Tail?.SeqNum})");

    // Test 3: Simple Append
    Console.Write("3. Appending single record... ");
    var receipt = await stream.AppendAsync(new TestEvent
    {
        Type = "test",
        Message = "Hello from .NET SDK!",
        Timestamp = DateTimeOffset.UtcNow
    });
    Console.WriteLine($"OK (seq: {receipt.SequenceNumber})");

    // Test 4: Batch Append
    Console.Write("4. Appending batch (10 records)... ");
    var batchEvents = Enumerable.Range(1, 10).Select(i => new TestEvent
    {
        Type = "batch",
        Message = $"Batch event {i}",
        Timestamp = DateTimeOffset.UtcNow
    });
    var batchResponse = await stream.AppendBatchAsync(batchEvents);
    Console.WriteLine($"OK (seq: {batchResponse.StartSequenceNumber} - {batchResponse.EndSequenceNumber})");

    // Test 5: AppendSession (high throughput)
    Console.Write("5. Testing AppendSession (100 records)... ");
    await using (var session = stream.OpenAppendSession(new AppendSessionOptions
    {
        BatchSize = 20,
        BatchTimeout = TimeSpan.FromMilliseconds(50)
    }))
    {
        for (int i = 0; i < 100; i++)
        {
            await session.AppendAsync(new TestEvent
            {
                Type = "session",
                Message = $"Session event {i}",
                Timestamp = DateTimeOffset.UtcNow
            });
        }
        await session.FlushAsync();
        Console.WriteLine($"OK (sent: {session.TotalSent})");
    }

    // Test 6: Get final stream info
    Console.Write("6. Final stream info... ");
    info = await stream.GetInfoAsync();
    Console.WriteLine($"OK (storage: {info.StorageClass})");

    // Test 6b: List streams via basin.Streams
    Console.Write("6b. Listing streams... ");
    var streamsResponse = await basinRef.Streams.ListAsync(prefix: "sdk-test");
    Console.WriteLine($"OK (count: {streamsResponse.Streams.Count})");

    // Test 6c: Check tail after appends
    Console.Write("6c. Check tail after appends... ");
    tail = await stream.CheckTailAsync();
    Console.WriteLine($"OK (seqNum: {tail.Tail?.SeqNum})");

    // Note: Read tests skipped - SSE streaming requires further work

    // Test 7: Delete stream (cleanup)
    Console.Write("7. Deleting test stream... ");
    await stream.DeleteAsync();
    Console.WriteLine("OK");

    Console.WriteLine("\n=== All tests passed! ===");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"\nFAILED: {ex.GetType().Name}: {ex.Message}");
    if (ex.InnerException != null)
        Console.WriteLine($"  Inner: {ex.InnerException.Message}");

    // Try to cleanup
    try
    {
        var s2 = new S2Client(new S2Options { AccessToken = token });
        await s2.Basin(basin).Stream(testStreamName).DeleteAsync();
        Console.WriteLine("  (cleanup: test stream deleted)");
    }
    catch { }

    return 1;
}

record TestEvent
{
    public string Type { get; init; } = "";
    public string Message { get; init; } = "";
    public DateTimeOffset Timestamp { get; init; }
}
