using Databento.Client.Builders;
using Databento.Client.Models;

Console.WriteLine("================================================================================");
Console.WriteLine("Instrument Definition Decoder Example");
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

// Query parameters - get definitions for a recent date
var start = DateTimeOffset.Parse("2023-11-14");
var end = DateTimeOffset.Parse("2023-11-15");

Console.WriteLine("Query Parameters:");
Console.WriteLine($"  Dataset:  GLBX.MDP3");
Console.WriteLine($"  Symbols:  ALL_SYMBOLS (all instruments)");
Console.WriteLine($"  Schema:   Definition");
Console.WriteLine($"  Date:     {start:yyyy-MM-dd}");
Console.WriteLine($"  Limit:    First 20 records");
Console.WriteLine();

Console.WriteLine("Decoding instrument definitions...");
Console.WriteLine();

var count = 0;
var optionCount = 0;
await foreach (var record in client.GetRangeAsync(
    dataset: "GLBX.MDP3",
    schema: Schema.Definition,
    symbols: new[] { "ALL_SYMBOLS" },
    startTime: start,
    endTime: end))
{
    if (record is InstrumentDefMessage def)
    {
        count++;

        // Also collect some actual options with strike prices
        if ((def.InstrumentClass == InstrumentClass.Call || def.InstrumentClass == InstrumentClass.Put)
            && def.StrikePriceDecimal.HasValue  // Now using nullable property!
            && optionCount < 5)
        {
            optionCount++;
            var expiry = def.Expiration > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(def.Expiration / 1_000_000).ToString("yyyy-MM-dd")
                : "N/A";

            Console.WriteLine($"[OPTION] {def.RawSymbol,-25} {def.InstrumentClass}");
            Console.WriteLine($"         Strike: ${def.StrikePriceDecimal.Value:F2}, Expiry: {expiry}");

            // Show all price decimal properties (now nullable - null = UNDEF_PRICE)
            if (def.HighLimitPriceDecimal.HasValue)
                Console.WriteLine($"         High Limit: ${def.HighLimitPriceDecimal.Value:F2}");
            if (def.LowLimitPriceDecimal.HasValue)
                Console.WriteLine($"         Low Limit: ${def.LowLimitPriceDecimal.Value:F2}");
            if (def.MinPriceIncrementDecimal.HasValue)
                Console.WriteLine($"         Tick Size: ${def.MinPriceIncrementDecimal.Value:F9}");

            Console.WriteLine($"         Raw: StrikePrice={def.StrikePrice}");
            Console.WriteLine();
        }

        // Display first 20 instruments with detailed info
        if (count <= 20)
        {
            Console.WriteLine($"[{count,3}] {def.RawSymbol,-25} Class: {def.InstrumentClass,-15} Asset: {def.Asset}");

            // Show additional details for interesting instruments
            if (count <= 5)
            {
                Console.WriteLine($"     └─ Exchange: {def.Exchange}, Currency: {def.Currency}");

                // Demonstrate nullable handling (null = UNDEF_PRICE sentinel)
                if (def.StrikePriceDecimal.HasValue)
                {
                    Console.WriteLine($"     └─ Strike Price: ${def.StrikePriceDecimal.Value:F2}");
                }
                else if (def.StrikePrice == long.MaxValue)
                {
                    Console.WriteLine($"     └─ Strike Price: null (UNDEF_PRICE sentinel)");
                }
                else if (def.StrikePrice == 0)
                {
                    Console.WriteLine($"     └─ Strike Price: 0 (not applicable)");
                }
            }
        }
        else if (count == 21)
        {
            Console.WriteLine("... (processing remaining records, will show summary)");
        }
    }
}

Console.WriteLine();
Console.WriteLine("================================================================================");
Console.WriteLine("SUMMARY");
Console.WriteLine("================================================================================");
Console.WriteLine($"Total instrument definitions decoded: {count}");
Console.WriteLine($"Options with valid strike prices found: {optionCount}");
Console.WriteLine();
Console.WriteLine("Key Features Demonstrated:");
Console.WriteLine("  1. InstrumentClass field correctly populated (Issue #4 fix)");
Console.WriteLine("  2. Decimal price conversion properties (nullable decimal?):");
Console.WriteLine("     - StrikePriceDecimal, HighLimitPriceDecimal, LowLimitPriceDecimal");
Console.WriteLine("     - MaxPriceVariationDecimal, MinPriceIncrementDecimal, etc.");
Console.WriteLine("     - Returns null when raw value is UNDEF_PRICE (long.MaxValue)");
Console.WriteLine("  3. UNDEF_PRICE sentinel properly handled as null");
Console.WriteLine("  4. All prices use fixed-point format: divide by 1e9 for decimal");
Console.WriteLine("  5. Prices can be negative (e.g., calendar spreads)");
Console.WriteLine();
Console.WriteLine("DBN Specification:");
Console.WriteLine("  - All prices stored as signed int64 with 9 decimal places (1e-9 precision)");
Console.WriteLine("  - UNDEF_PRICE = 9223372036854775807 (INT64_MAX / long.MaxValue) → null");
Console.WriteLine("  - Example: 5411750000000 ÷ 1e9 = 5411.75");
Console.WriteLine("  - Example: 9223372036854775807 (UNDEF_PRICE) → null");
Console.WriteLine();

return 0;
