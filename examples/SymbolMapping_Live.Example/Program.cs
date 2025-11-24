// Symbol Mapping with Live API Example
//
// This example demonstrates how the Live API automatically includes SymbolMappingMessage
// records in the data stream, eliminating the need for SymbologyResolveAsync().
//
// CRITICAL DIFFERENCE from Historical API:
// - Live API: ALWAYS sends SymbolMappingMessage in stream ✅
// - Historical API: NEVER sends SymbolMappingMessage ❌
//
// This example uses REPLAY mode so it works anytime (doesn't require market to be open).

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Databento.Client.Builders;
using Databento.Client.Models;

Console.WriteLine("================================================================================");
Console.WriteLine("Symbol Mapping with Live API Example (Replay Mode)");
Console.WriteLine("================================================================================");
Console.WriteLine();

// Get API key from environment
var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("ERROR: DATABENTO_API_KEY environment variable not set!");
    return 1;
}

// Create Live client
await using var client = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .WithDataset("EQUS.MINI")
    .Build();

Console.WriteLine("✓ Created LiveClient");
Console.WriteLine();

// Symbol map built from SymbolMappingMessage records in the stream
var symbolMap = new Dictionary<uint, string>();
var mappingCount = 0;
var tradeCount = 0;
const int displayLimit = 20;

// Subscribe to data events
client.DataReceived += (sender, e) =>
{
    if (e.Record is SymbolMappingMessage mapping)
    {
        mappingCount++;

        // CRITICAL: Use STypeOutSymbol (NOT STypeInSymbol)
        // STypeOutSymbol = actual ticker ("NVDA", "AAPL", etc.)
        // STypeInSymbol = subscription string (would be "NVDA" for single symbols)
        symbolMap[mapping.InstrumentId] = mapping.STypeOutSymbol;

        Console.WriteLine($"[MAPPING #{mappingCount}] InstrumentId {mapping.InstrumentId,5} → {mapping.STypeOutSymbol}");
    }
    else if (e.Record is TradeMessage trade)
    {
        tradeCount++;

        // Look up ticker symbol from instrument ID
        var symbol = symbolMap.GetValueOrDefault(
            trade.InstrumentId,
            $"ID:{trade.InstrumentId}"); // Fallback if not mapped yet

        if (tradeCount <= displayLimit)
        {
            Console.WriteLine($"[TRADE #{tradeCount,3}] {symbol,-6} @ ${trade.PriceDecimal,8:F2} x {trade.Size,5} shares");
        }
        else if (tradeCount == displayLimit + 1)
        {
            Console.WriteLine("... (suppressing further output, continuing to count)");
        }
    }
};

// Subscribe to symbols in REPLAY mode
var replayStart = new DateTimeOffset(2024, 11, 18, 9, 30, 0, TimeSpan.FromHours(-5)); // Market open
var symbols = new[] { "NVDA", "AAPL", "MSFT" };

Console.WriteLine("Subscription Parameters:");
Console.WriteLine($"  Dataset:      EQUS.MINI");
Console.WriteLine($"  Symbols:      {string.Join(", ", symbols)}");
Console.WriteLine($"  Schema:       Trades");
Console.WriteLine($"  Mode:         REPLAY (historical data playback)");
Console.WriteLine($"  Replay Start: {replayStart:yyyy-MM-dd HH:mm:ss zzz}");
Console.WriteLine();

await client.SubscribeAsync(
    dataset: "EQUS.MINI",
    schema: Schema.Trades,
    symbols: symbols,
    startTime: replayStart);

Console.WriteLine("✓ Subscribed to 3 symbols");
Console.WriteLine();

await client.StartAsync();
Console.WriteLine("✓ Stream started");
Console.WriteLine();

const int runDurationSeconds = 10;
Console.WriteLine($"Receiving data (will run for {runDurationSeconds} seconds of replay)...");
Console.WriteLine();

// Let it run for a bit to collect data
await Task.Delay(TimeSpan.FromSeconds(runDurationSeconds));

Console.WriteLine();
Console.WriteLine("================================================================================");
Console.WriteLine("SUMMARY");
Console.WriteLine("================================================================================");
Console.WriteLine($"SymbolMappingMessages received: {mappingCount}");
Console.WriteLine($"Trade messages received:        {tradeCount}");
Console.WriteLine($"Symbol map size:                {symbolMap.Count} instruments");
Console.WriteLine();

if (symbolMap.Count > 0)
{
    Console.WriteLine("Mapped instruments:");
    foreach (var (id, symbol) in symbolMap)
    {
        Console.WriteLine($"  {symbol,-6} → Instrument ID {id}");
    }
    Console.WriteLine();
}

Console.WriteLine("Key Takeaways:");
Console.WriteLine("  1. Live API automatically sends SymbolMappingMessage in the stream");
Console.WriteLine("  2. Build the symbol map by handling SymbolMappingMessage as data arrives");
Console.WriteLine("  3. No need for SymbologyResolveAsync() with Live API");
Console.WriteLine("  4. Use STypeOutSymbol (NOT STypeInSymbol) to get ticker symbols");
Console.WriteLine("  5. Works for both specific symbols AND ALL_SYMBOLS subscriptions");
Console.WriteLine();

Console.WriteLine("Comparison:");
Console.WriteLine("  Live API:       SymbolMappingMessage in stream ✅ (this example)");
Console.WriteLine("  Historical API: NO SymbolMappingMessage ❌ (use SymbologyResolveAsync)");
Console.WriteLine();

return 0;
