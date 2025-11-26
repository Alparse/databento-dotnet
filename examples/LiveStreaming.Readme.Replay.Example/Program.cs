using Databento.Client.Builders;
using Databento.Client.Models;

// REPLAY version that works outside market hours

// Get API key from environment variable (secure)
var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
    ?? throw new InvalidOperationException("DATABENTO_API_KEY environment variable not set");

// Calculate most recent market open for valid replay time
var marketOpen = GetMostRecentMarketOpen();
Console.WriteLine($"Using most recent market open: {marketOpen:yyyy-MM-dd HH:mm:ss} ET");
Console.WriteLine();

// Create live client
await using var client = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .Build();

// Subscribe to events
client.DataReceived += (sender, e) =>
{
    Console.WriteLine($"Received: {e.Record}");
};

// Use replay mode for testing outside market hours
var replayStart = marketOpen;
Console.WriteLine($"Replay start time: {replayStart:yyyy-MM-dd HH:mm:ss zzz}");
Console.WriteLine();

// Subscribe to NVDA trades with replay
await client.SubscribeAsync(
    dataset: "EQUS.MINI",
    schema: Schema.Trades,
    symbols: new[] { "NVDA" },
    startTime: replayStart  // This enables replay mode
);

// Start streaming
await client.StartAsync();

// Stream records using IAsyncEnumerable
var count = 0;
await foreach (var record in client.StreamAsync())
{
    // Process records
    if (record is TradeMessage trade)
    {
        count++;
        Console.WriteLine($"Trade #{count}: " +
            $"InstrumentId={trade.InstrumentId} " +
            $"Price=${trade.PriceDecimal:F2} " +
            $"Size={trade.Size} " +
            $"Side={trade.Side}");
        if (count >= 10) break;  // Limit for demo
    }
}

Console.WriteLine($"\nReceived {count} trades (REPLAY mode)");

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
