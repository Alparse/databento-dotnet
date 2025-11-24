// Symbol Mapping with Historical API Example
//
// This example demonstrates how to properly handle symbol-to-instrument-ID mapping
// when using the Historical API.
//
// CRITICAL: Historical API NEVER sends SymbolMappingMessage records
// - Historical API + specific symbols: NO SymbolMappingMessage ❌
// - Historical API + ALL_SYMBOLS: NO SymbolMappingMessage ❌
// - Live API (any symbols): ALWAYS includes SymbolMappingMessage ✅
//
// Solution: Use SymbologyResolveAsync() to pre-populate the symbol map before streaming data.

using System;
using System.Collections.Generic;
using Databento.Client.Builders;
using Databento.Client.Models;

Console.WriteLine("================================================================================");
Console.WriteLine("Symbol Mapping with Historical API Example");
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

// Query parameters
var startTime = new DateTimeOffset(2024, 11, 18, 9, 30, 0, TimeSpan.FromHours(-5)); // 09:30:00 EST
var endTime = startTime.AddMinutes(1); // 09:31:00 EST
var symbols = new[] { "NVDA", "AAPL", "MSFT" };

Console.WriteLine("Query Parameters:");
Console.WriteLine($"  Dataset:    EQUS.MINI");
Console.WriteLine($"  Symbols:    {string.Join(", ", symbols)}");
Console.WriteLine($"  Schema:     Trades");
Console.WriteLine($"  Start Time: {startTime:yyyy-MM-dd HH:mm:ss zzz}");
Console.WriteLine($"  End Time:   {endTime:yyyy-MM-dd HH:mm:ss zzz}");
Console.WriteLine($"  Duration:   1 minute");
Console.WriteLine();

// STEP 1: Resolve symbols to instrument IDs
// This is REQUIRED because Historical API NEVER sends SymbolMappingMessage
// (regardless of whether you query specific symbols or ALL_SYMBOLS)
Console.WriteLine("Step 1: Resolving symbols to instrument IDs...");
var queryDate = DateOnly.FromDateTime(startTime.Date);
var resolution = await client.SymbologyResolveAsync(
    "EQUS.MINI",
    symbols,
    SType.RawSymbol,
    SType.InstrumentId,
    queryDate,
    queryDate.AddDays(1)); // End date must be after start date

// Build symbol map (InstrumentId -> Symbol)
var symbolMap = new Dictionary<uint, string>();
foreach (var (inputSymbol, intervals) in resolution.Mappings)
{
    foreach (var interval in intervals)
    {
        if (uint.TryParse(interval.Symbol, out var instrumentId))
        {
            symbolMap[instrumentId] = inputSymbol;
            Console.WriteLine($"  {inputSymbol,-6} → Instrument ID {instrumentId}");
        }
    }
}

Console.WriteLine($"✓ Resolved {symbolMap.Count} instrument IDs");
Console.WriteLine();

// STEP 2: Stream trade data
Console.WriteLine("Step 2: Streaming trade data (showing first 20 trades)...");
Console.WriteLine();

var tradeCount = 0;
const int displayLimit = 20;

await foreach (var record in client.GetRangeAsync(
    dataset: "EQUS.MINI",
    schema: Schema.Trades,
    symbols: symbols,
    startTime: startTime,
    endTime: endTime))
{
    if (record is TradeMessage trade)
    {
        tradeCount++;

        // Look up ticker symbol from instrument ID
        var symbol = symbolMap.GetValueOrDefault(
            trade.InstrumentId,
            trade.InstrumentId.ToString()); // Fallback to ID if not found

        if (tradeCount <= displayLimit)
        {
            Console.WriteLine($"[{tradeCount,3}] {symbol,-6} (ID: {trade.InstrumentId,5}) @ ${trade.PriceDecimal,8:F2} x {trade.Size,5} shares");
        }
        else if (tradeCount == displayLimit + 1)
        {
            Console.WriteLine($"... (suppressing further output, continuing to count)");
        }
    }
}

Console.WriteLine();
Console.WriteLine("================================================================================");
Console.WriteLine("SUMMARY");
Console.WriteLine("================================================================================");
Console.WriteLine($"Total trades received: {tradeCount}");
Console.WriteLine($"Symbols resolved:      {symbolMap.Count}");
Console.WriteLine();

Console.WriteLine("Key Takeaways:");
Console.WriteLine("  1. Historical API NEVER sends SymbolMappingMessage (specific symbols OR ALL_SYMBOLS)");
Console.WriteLine("  2. Live API ALWAYS sends SymbolMappingMessage (specific symbols OR ALL_SYMBOLS)");
Console.WriteLine("  3. Use SymbologyResolveAsync() to map symbols → instrument IDs for Historical API");
Console.WriteLine("  4. Build a Dictionary<uint, string> to look up symbols during streaming");
Console.WriteLine();

return 0;
