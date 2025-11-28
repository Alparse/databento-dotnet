// Historical Trade Messages Example
//
// This example demonstrates how to:
// 1. Query historical trade messages for specific symbols (NVDA, AAPL, MSFT)
// 2. Resolve instrument IDs to ticker symbols using SymbologyResolveAsync
// 3. Display full TradeMessage details including price, size, timestamp, etc.
// 4. Process 1 minute of trade data from EQUS.MINI dataset

using System;
using System.Collections.Generic;
using System.Linq;
using Databento.Client.Builders;
using Databento.Client.Models;

Console.WriteLine("================================================================================");
Console.WriteLine("Historical Trade Messages Example - EQUS.MINI");
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

// Query for 1 minute of trade data during market hours
var startTime = new DateTimeOffset(2024, 11, 18, 9, 30, 0, TimeSpan.FromHours(-5)); // 09:30:00 EST
var endTime = startTime.AddMinutes(1); // 09:31:00 EST

// Symbols to query
var symbols = new[] { "NVDA", "AAPL", "MSFT" };

Console.WriteLine("Query Parameters:");
Console.WriteLine($"  Dataset:    EQUS.MINI");
Console.WriteLine($"  Symbols:    {string.Join(", ", symbols)}");
Console.WriteLine($"  Schema:     Trades");
Console.WriteLine($"  Start Time: {startTime:yyyy-MM-dd HH:mm:ss zzz}");
Console.WriteLine($"  End Time:   {endTime:yyyy-MM-dd HH:mm:ss zzz}");
Console.WriteLine($"  Duration:   1 minute");
Console.WriteLine();

// Resolve symbols to instrument IDs for this date
Console.WriteLine("Resolving symbols to instrument IDs...");
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
        }
    }
}

Console.WriteLine($"✓ Resolved {symbolMap.Count} instrument IDs");
Console.WriteLine();

Console.WriteLine("Fetching trade messages...");
Console.WriteLine();

var tradeCount = 0;

await foreach (var record in client.GetRangeAsync(
    dataset: "EQUS.MINI",
    schema: Schema.Trades,
    symbols: symbols,
    startTime: startTime,
    endTime: endTime))
{
    // Display trade messages
    if (record is TradeMessage trade)
    {
        tradeCount++;

        // Resolve ticker symbol from instrument ID
        var symbol = symbolMap.TryGetValue(trade.InstrumentId, out var sym) ? sym : $"ID:{trade.InstrumentId}";

        // Display full trade message details
        Console.WriteLine($"[Trade #{tradeCount}]");
        Console.WriteLine($"  Symbol:        {symbol}");
        Console.WriteLine($"  Instrument ID: {trade.InstrumentId}");
        Console.WriteLine($"  Price:         ${trade.PriceDecimal:N2}");
        Console.WriteLine($"  Size:          {trade.Size:N0} shares");
        Console.WriteLine($"  Timestamp:     {trade.Timestamp:yyyy-MM-dd HH:mm:ss.ffffff zzz}");
        Console.WriteLine($"  Side:          {trade.Side}");
        Console.WriteLine($"  Action:        {trade.Action}");
        Console.WriteLine($"  Publisher ID:  {trade.PublisherId}");
        Console.WriteLine($"  Sequence:      {trade.Sequence}");
        Console.WriteLine();
    }
}

Console.WriteLine("================================================================================");
Console.WriteLine("SUMMARY");
Console.WriteLine("================================================================================");
Console.WriteLine($"Symbol mappings received: {symbolMap.Count}");
Console.WriteLine($"Trade messages received:  {tradeCount}");
Console.WriteLine();

if (symbolMap.Count > 0)
{
    Console.WriteLine("Mapped instruments:");
    foreach (var (id, symbol) in symbolMap.OrderBy(kvp => kvp.Value))
    {
        Console.WriteLine($"  {symbol,-6} → Instrument ID {id}");
    }
    Console.WriteLine();
}

if (tradeCount > 0)
{
    Console.WriteLine("Each trade message contains:");
    Console.WriteLine("  - Symbol (ticker symbol resolved from instrument ID)");
    Console.WriteLine("  - Instrument ID (unique identifier for the security)");
    Console.WriteLine("  - Price (converted to decimal from fixed-point format)");
    Console.WriteLine("  - Size (volume in shares)");
    Console.WriteLine("  - Timestamp (with microsecond precision)");
    Console.WriteLine("  - Side (Ask/Bid/None)");
    Console.WriteLine("  - Action (Trade/Cancel/etc.)");
    Console.WriteLine("  - Publisher ID (data venue)");
    Console.WriteLine("  - Sequence (message sequence number)");
    Console.WriteLine();
    Console.WriteLine("Symbol Resolution:");
    Console.WriteLine("  Historical API does NOT send SymbolMappingMessage records in the stream.");
    Console.WriteLine("  This example uses SymbologyResolveAsync() to pre-populate the symbol map");
    Console.WriteLine("  before streaming data. This allows displaying human-readable ticker symbols");
    Console.WriteLine("  alongside instrument IDs in the output.");
}
else
{
    Console.WriteLine("No trades found for this time period.");
    Console.WriteLine("Try a different date/time during market hours.");
}

Console.WriteLine();
Console.WriteLine("=== Historical Trade Messages Example Complete ===");

return 0;
