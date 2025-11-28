using System.Collections.Concurrent;
using Databento.Client.Builders;
using Databento.Client.Models;

Console.WriteLine("================================================================================");
Console.WriteLine("Intraday Replay Example - LiveClient with StreamAsync()");
Console.WriteLine("================================================================================");
Console.WriteLine();
Console.WriteLine("This example demonstrates:");
Console.WriteLine("  • LiveClient (streaming, push-based, non-blocking)");
Console.WriteLine("  • Replay mode from most recent market open");
Console.WriteLine("  • Event-driven data handling");
Console.WriteLine("  • StreamAsync() to keep program alive");
Console.WriteLine();

var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
    ?? throw new InvalidOperationException("DATABENTO_API_KEY not set");

// Calculate most recent market open
var replayStartTime = new DateTimeOffset(2025, 11, 26, 14, 30, 0, TimeSpan.Zero);

Console.WriteLine($"Replay Configuration:");
Console.WriteLine($"  Start Time:  {replayStartTime:yyyy-MM-dd HH:mm:ss zzz}");
Console.WriteLine($"  Day of Week: {replayStartTime.DayOfWeek}");
Console.WriteLine();

// Create live client
await using var client = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .WithDataset("EQUS.MINI")
    .Build();

Console.WriteLine("✓ Created LiveClient (streaming mode)");
Console.WriteLine();

// Subscribe to EVENTS (data pushes to you)
var tradeCount = 0;
client.DataReceived += (sender, e) =>
{
    // Process record as it arrives
    if (e.Record is TradeMessage trade)
    {
        Interlocked.Increment(ref tradeCount);

        // Display first 10 trades
        if (tradeCount <= 10)
        {
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(trade.TimestampNs / 1_000_000);
            Console.WriteLine($"[{tradeCount,3}] Trade @ {timestamp:HH:mm:ss.fff} - ${trade.PriceDecimal,8:F2}");
        }
        else if (tradeCount == 11)
        {
            Console.WriteLine("... (continuing to collect data)");
        }
    }
};

client.ErrorOccurred += (sender, e) =>
{
    Console.WriteLine($"Error: {e.Exception.Message}");
};

// Subscribe with replay mode
await client.SubscribeAsync(
    dataset: "EQUS.MINI",
    schema: Schema.Trades,
    symbols: new[] { "NVDA", "AAPL" },  // Subscribe to 2 symbols for more data
    startTime: replayStartTime  // Request replay from market open
);

Console.WriteLine("✓ Subscribed to NVDA, AAPL trades with replay");
Console.WriteLine();

// Start - this spawns background thread that PUSHES data
var metadata = await client.StartAsync();

Console.WriteLine($"✓ Stream started");
Console.WriteLine($"  Dataset:     {metadata.Dataset}");
Console.WriteLine($"  Symbols:     {string.Join(", ", metadata.Symbols)}");
Console.WriteLine($"  Replay from: {DateTimeOffset.FromUnixTimeMilliseconds(metadata.Start / 1_000_000):yyyy-MM-dd HH:mm:ss} UTC");
Console.WriteLine();
Console.WriteLine("Collecting 1000 records...");
Console.WriteLine();

// CRITICAL: Keep the program alive to receive data
// Option 1: StreamAsync() - consume via IAsyncEnumerable
var count = 0;
await foreach (var record in client.StreamAsync())
{
    count++;

    // Break after collecting 1000 records
    if (count >= 1000)
    {
        Console.WriteLine();
        Console.WriteLine($"✓ Received {count} records, stopping...");
        break;
    }
}

await client.StopAsync();

Console.WriteLine();
Console.WriteLine("================================================================================");
Console.WriteLine("SUMMARY");
Console.WriteLine("================================================================================");
Console.WriteLine($"Total records received:  {count}");
Console.WriteLine($"Trade messages:          {tradeCount}");
Console.WriteLine($"Other messages:          {count - tradeCount}");
Console.WriteLine();

Console.WriteLine("Key Concepts Demonstrated:");
Console.WriteLine("  • LiveClient = Streaming (push-based, non-blocking)");
Console.WriteLine("  • Event handlers fire automatically as data arrives");
Console.WriteLine("  • StreamAsync() keeps program alive to receive pushed data");
Console.WriteLine("  • Replay mode works anytime (no market hours required)");
Console.WriteLine();

Console.WriteLine("Alternative Approaches:");
Console.WriteLine("  1. StreamAsync() - Active iteration (used in this example)");
Console.WriteLine("  2. Task.Delay()  - Passive time-based collection");
Console.WriteLine("  3. BlockUntilStoppedAsync() - Block until stopped or timeout");
Console.WriteLine();

return 0;

/// <summary>
/// Calculate the most recent US equity market open (9:30 AM ET)
/// Handles weekends by going back to Friday
/// </summary>
static DateTimeOffset GetMostRecentMarketOpen()
{
    // US Eastern Time Zone
    var etZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

    // Get current time in ET
    var nowEt = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, etZone);

    // Market opens at 9:30 AM ET
    var marketOpenTime = new TimeSpan(9, 30, 0);

    // Start with today's date
    var candidateDate = nowEt.Date;

    // Handle weekends
    if (candidateDate.DayOfWeek == DayOfWeek.Saturday)
    {
        candidateDate = candidateDate.AddDays(-1); // Friday
    }
    else if (candidateDate.DayOfWeek == DayOfWeek.Sunday)
    {
        candidateDate = candidateDate.AddDays(-2); // Friday
    }
    else if (nowEt.TimeOfDay < marketOpenTime)
    {
        // Before market open today - go back to previous trading day
        candidateDate = candidateDate.AddDays(-1);

        // If that's a weekend, go back to Friday
        if (candidateDate.DayOfWeek == DayOfWeek.Saturday)
        {
            candidateDate = candidateDate.AddDays(-1); // Friday
        }
        else if (candidateDate.DayOfWeek == DayOfWeek.Sunday)
        {
            candidateDate = candidateDate.AddDays(-2); // Friday
        }
    }

    // Create market open datetime in ET
    var marketOpen = new DateTimeOffset(
        candidateDate.Year,
        candidateDate.Month,
        candidateDate.Day,
        9, 30, 0, // 9:30 AM
        etZone.GetUtcOffset(candidateDate));

    return marketOpen;
}
