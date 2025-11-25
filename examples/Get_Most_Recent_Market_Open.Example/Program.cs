using Databento.Client.Builders;
using Databento.Client.Models;

Console.WriteLine("================================================================================");
Console.WriteLine("Get Most Recent Market Open Example");
Console.WriteLine("================================================================================");
Console.WriteLine();
Console.WriteLine("This example demonstrates:");
Console.WriteLine("  • Calculating the most recent US equity market open time");
Console.WriteLine("  • Handling weekends and market hours");
Console.WriteLine("  • Querying data from the most recent trading session");
Console.WriteLine();

// Get API key from environment
var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("ERROR: DATABENTO_API_KEY environment variable not set!");
    Console.WriteLine();
    Console.WriteLine("Set your API key:");
    Console.WriteLine("  Windows: setx DATABENTO_API_KEY \"your-key\"");
    Console.WriteLine("  Linux/Mac: export DATABENTO_API_KEY=\"your-key\"");
    return 1;
}

// Calculate most recent market open
var marketOpen = GetMostRecentMarketOpen();

Console.WriteLine($"Current Time:        {DateTime.Now:yyyy-MM-dd HH:mm:ss} (Local)");
Console.WriteLine($"Most Recent Open:    {marketOpen:yyyy-MM-dd HH:mm:ss} ET");
Console.WriteLine($"Day of Week:         {marketOpen.DayOfWeek}");
Console.WriteLine();

// Calculate a 5-minute window from market open
var startTime = marketOpen;
var endTime = marketOpen.AddMinutes(5);

Console.WriteLine($"Querying first 5 minutes of trading:");
Console.WriteLine($"  Start:  {startTime:yyyy-MM-dd HH:mm:ss} ET");
Console.WriteLine($"  End:    {endTime:yyyy-MM-dd HH:mm:ss} ET");
Console.WriteLine();

// Create Historical client
await using var client = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();

Console.WriteLine("✓ Created HistoricalClient");
Console.WriteLine();

// Query trades from the most recent market open
Console.WriteLine("Fetching trades for NVDA from most recent market open...");
Console.WriteLine();

var count = 0;
var firstTrade = true;
TradeMessage? firstTradeMsg = null;
TradeMessage? lastTradeMsg = null;

await foreach (var record in client.GetRangeAsync(
    dataset: "EQUS.MINI",
    schema: Schema.Trades,
    symbols: new[] { "NVDA" },
    startTime: startTime,
    endTime: endTime))
{
    if (record is TradeMessage trade)
    {
        count++;
        lastTradeMsg = trade;

        if (firstTrade)
        {
            firstTradeMsg = trade;
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(trade.TimestampNs / 1_000_000);
            Console.WriteLine($"First Trade:");
            Console.WriteLine($"  Time:   {timestamp:HH:mm:ss.fff} ET");
            Console.WriteLine($"  Price:  ${trade.PriceDecimal:F2}");
            Console.WriteLine($"  Size:   {trade.Size}");
            Console.WriteLine();
            firstTrade = false;
        }

        // Display first 10 trades
        if (count <= 10)
        {
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(trade.TimestampNs / 1_000_000);
            Console.WriteLine($"[{count,3}] {timestamp:HH:mm:ss.fff} - ${trade.PriceDecimal,8:F2} x {trade.Size,6}");
        }
        else if (count == 11)
        {
            Console.WriteLine("... (processing remaining trades)");
        }
    }
}

Console.WriteLine();
Console.WriteLine("================================================================================");
Console.WriteLine("SUMMARY");
Console.WriteLine("================================================================================");
Console.WriteLine($"Market Open Time:        {marketOpen:yyyy-MM-dd HH:mm:ss} ET");
Console.WriteLine($"Query Window:            5 minutes");
Console.WriteLine($"Total Trades:            {count}");
Console.WriteLine();

if (firstTradeMsg != null && lastTradeMsg != null)
{
    var firstTime = DateTimeOffset.FromUnixTimeMilliseconds(firstTradeMsg.TimestampNs / 1_000_000);
    var lastTime = DateTimeOffset.FromUnixTimeMilliseconds(lastTradeMsg.TimestampNs / 1_000_000);
    var duration = lastTime - firstTime;

    Console.WriteLine("Trading Activity:");
    Console.WriteLine($"  First Trade:  {firstTime:HH:mm:ss.fff} ET @ ${firstTradeMsg.PriceDecimal:F2}");
    Console.WriteLine($"  Last Trade:   {lastTime:HH:mm:ss.fff} ET @ ${lastTradeMsg.PriceDecimal:F2}");
    Console.WriteLine($"  Duration:     {duration.TotalSeconds:F2} seconds");
    Console.WriteLine($"  Avg Rate:     {count / duration.TotalSeconds:F2} trades/second");
}
Console.WriteLine();

Console.WriteLine("Use Cases:");
Console.WriteLine("  • Market open analysis");
Console.WriteLine("  • Volatility studies during opening minutes");
Console.WriteLine("  • Gap analysis (compare to previous close)");
Console.WriteLine("  • Opening range breakout strategies");
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

    // If we're before market open today, use today
    // If we're after market open today, use today
    // But if it's a weekend, go back to Friday

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
