using Databento.Client.Builders;
using Databento.Client.Historical;
using Databento.Client.Models;
using Databento.Client.Models.Symbology;
using FuturesFilter.Example;

// Uncomment this line to discover all available parent symbols
//await GetParentSymbols.RunExampleAsync();
//return 0;

Console.WriteLine("=== Databento Futures Filter Example (SType.Parent) ===\n");
Console.WriteLine("This example demonstrates filtering for futures only using parent symbology.");
Console.WriteLine("We'll query ES.FUT (all E-mini S&P 500 futures) from GLBX.MDP3\n");

// Get API key from environment variable
var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("ERROR: DATABENTO_API_KEY environment variable not set");
    return 1;
}

Console.WriteLine($"✓ API key found: {apiKey.Substring(0, 8)}... (masked)\n");

try
{
    // Create historical client
    await using var client = new HistoricalClientBuilder()
        .WithApiKey(apiKey)
        .Build();

    Console.WriteLine("✓ Created Historical client\n");

    // Define time range (using a historical date to ensure data availability)
    var startTime = new DateTimeOffset(2024, 11, 1, 13, 30, 0, TimeSpan.FromHours(-5)); // 1:30 PM EST
    var endTime = startTime.AddMinutes(5); // 5 minutes of data

    Console.WriteLine($"Query Parameters:");
    Console.WriteLine($"  Dataset:    GLBX.MDP3");
    Console.WriteLine($"  Schema:     Trades");
    Console.WriteLine($"  Symbol:     ES.FUT (all E-mini S&P 500 futures)");
    Console.WriteLine($"  SType In:   Parent");
    Console.WriteLine($"  SType Out:  InstrumentId (required by GLBX.MDP3)");
    Console.WriteLine($"  Start Time: {startTime:yyyy-MM-dd HH:mm:ss zzz}");
    Console.WriteLine($"  End Time:   {endTime:yyyy-MM-dd HH:mm:ss zzz}");
    Console.WriteLine($"  Limit:      100 records\n");

    Console.WriteLine("Fetching futures data...\n");

    var recordCount = 0;
    var instrumentsSeen = new HashSet<uint>();

    // Query using the new overload with SType.Parent
    // NOTE: For GLBX.MDP3, Parent → InstrumentId is the only supported output
    await foreach (var record in client.GetRangeAsync(
        dataset: "GLBX.MDP3",
        schema: Schema.Trades,
        symbols: new[] { "ES.FUT2" }, // Parent symbol for all ES futures
        startTime: startTime,
        endTime: endTime,
        stypeIn: SType.Parent,        // Interpret "ES.FUT" as parent symbol
        stypeOut: SType.InstrumentId, // Return instrument IDs (required)
        limit: 100))                   // Limit to 100 records for demo
    {
        recordCount++;

        if (record is TradeMessage trade)
        {
            // Track unique instrument IDs
            instrumentsSeen.Add(trade.InstrumentId);

            // Show first 10 trades
            if (recordCount <= 10)
            {
                Console.WriteLine($"  Trade #{recordCount}: InstrumentId={trade.InstrumentId}, " +
                                $"Price=${trade.PriceDecimal:F2}, " +
                                $"Size={trade.Size}, " +
                                $"Time={trade.Timestamp:HH:mm:ss.fff}");
            }
        }
    }

    Console.WriteLine($"\n✓ Query completed successfully!");
    Console.WriteLine($"\nSummary:");
    Console.WriteLine($"  Total records:         {recordCount}");
    Console.WriteLine($"  Unique ES instruments: {instrumentsSeen.Count}");
    Console.WriteLine($"\nNote: All returned data is from ES futures contracts only,");
    Console.WriteLine($"      filtered by using SType.Parent with 'ES.FUT'");
    Console.WriteLine($"\nSupported GLBX.MDP3 combinations:");
    Console.WriteLine($"  ✓ parent → instrument_id");
    Console.WriteLine($"  ✓ continuous → instrument_id");
    Console.WriteLine($"  ✓ raw_symbol → instrument_id");
    Console.WriteLine($"  ✓ instrument_id → raw_symbol");
    Console.WriteLine($"\nSee: https://databento.com/docs/standards-and-conventions/symbology");

    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"\n✗ Error: {ex.Message}");
    Console.WriteLine($"  Type: {ex.GetType().Name}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"  Inner: {ex.InnerException.Message}");
    }
    return 1;
}
