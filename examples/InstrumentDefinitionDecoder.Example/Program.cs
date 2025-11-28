using Databento.Client.Builders;
using Databento.Client.Models;

Console.WriteLine("================================================================================");
Console.WriteLine("OHLCV Bar Decoder Example");
Console.WriteLine("================================================================================");
Console.WriteLine();

// Get API key from environment
var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("ERROR: DATABENTO_API_KEY environment variable not set!");
    return 1;
}

// Create Historical client
await using var client = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();

Console.WriteLine("✓ Created HistoricalClient");
Console.WriteLine();

// Query parameters - OHLCV 1-second bars
var start = DateTimeOffset.Parse("2024-01-02T09:30:00-05:00");  // Market open
var end = DateTimeOffset.Parse("2024-01-02T09:35:00-05:00");    // 5 minutes of data

Console.WriteLine("Query Parameters:");
Console.WriteLine($"  Dataset:  EQUS.MINI");
Console.WriteLine($"  Symbols:  NVDA, TSLA, GE, MSFT");
Console.WriteLine($"  Schema:   OHLCV-1S (1-second bars)");
Console.WriteLine($"  Time:     {start:yyyy-MM-dd HH:mm:ss} to {end:yyyy-MM-dd HH:mm:ss} EST");
Console.WriteLine($"  Duration: 5 minutes");
Console.WriteLine();

Console.WriteLine("Decoding OHLCV bars...");
Console.WriteLine();

var count = 0;
var symbolCounts = new Dictionary<string, int>();

await foreach (var record in client.GetRangeAsync(
    dataset: "EQUS.MINI",
    schema: Schema.Ohlcv1S,
    symbols: new[] { "NVDA", "TSLA", "GE", "MSFT" },
    startTime: start,
    endTime: end))
{
    if (record is OhlcvMessage bar)
    {
        count++;

        // Track per-symbol counts
        var symbol = $"ID:{bar.InstrumentId}";  // Would need symbol mapping for actual symbol
        if (!symbolCounts.ContainsKey(symbol))
        {
            symbolCounts[symbol] = 0;
        }
        symbolCounts[symbol]++;

        // Display first 20 bars
        if (count <= 20)
        {
            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(bar.TimestampNs / 1_000_000);
            Console.WriteLine($"[{count,3}] InstrumentId: {bar.InstrumentId,5} @ {timestamp:HH:mm:ss}");
            Console.WriteLine($"     Open:   ${bar.OpenDecimal,8:F2}");
            Console.WriteLine($"     High:   ${bar.HighDecimal,8:F2}");
            Console.WriteLine($"     Low:    ${bar.LowDecimal,8:F2}");
            Console.WriteLine($"     Close:  ${bar.CloseDecimal,8:F2}");
            Console.WriteLine($"     Volume: {bar.Volume,8}");
            Console.WriteLine();
        }
        else if (count == 21)
        {
            Console.WriteLine("... (processing remaining bars, will show summary)");
            Console.WriteLine();
        }
    }
}

Console.WriteLine();
Console.WriteLine("================================================================================");
Console.WriteLine("SUMMARY");
Console.WriteLine("================================================================================");
Console.WriteLine($"Total OHLCV bars decoded: {count}");
Console.WriteLine();

if (symbolCounts.Count > 0)
{
    Console.WriteLine("Bars per instrument:");
    foreach (var kvp in symbolCounts.OrderByDescending(x => x.Value))
    {
        Console.WriteLine($"  {kvp.Key}: {kvp.Value} bars");
    }
    Console.WriteLine();
}

Console.WriteLine("Key Features Demonstrated:");
Console.WriteLine("  1. OHLCV-1S schema provides 1-second candlestick bars");
Console.WriteLine("  2. Each bar contains Open, High, Low, Close prices and Volume");
Console.WriteLine("  3. Decimal properties automatically convert fixed-point prices");
Console.WriteLine("  4. Timestamps use nanosecond precision");
Console.WriteLine("  5. Multiple symbols can be queried simultaneously");
Console.WriteLine();

Console.WriteLine("OHLCV Message Fields:");
Console.WriteLine("  - TimestampNs: Event timestamp in nanoseconds");
Console.WriteLine("  - InstrumentId: Numeric instrument identifier");
Console.WriteLine("  - Open/High/Low/Close: Price values (fixed-point int64)");
Console.WriteLine("  - OpenDecimal/HighDecimal/LowDecimal/CloseDecimal: Converted decimal prices");
Console.WriteLine("  - Volume: Trading volume for the bar period");
Console.WriteLine();

Console.WriteLine("=== OHLCV Bar Decoder Example Complete ===");
Console.WriteLine();

return 0;
