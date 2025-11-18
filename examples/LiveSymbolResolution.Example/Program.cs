using System.Collections.Concurrent;
using System.Diagnostics;
using Databento.Client.Builders;
using Databento.Client.Models;

namespace LiveSymbolResolution.Example;

/// <summary>
/// Demonstrates symbol resolution during live streaming with replay mode.
/// Shows manual ConcurrentDictionary approach for symbol mapping.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Symbol Resolution Example (Replay Mode) ===");
        Console.WriteLine();
        Console.WriteLine("This example demonstrates how to resolve InstrumentId → Ticker Symbol");
        Console.WriteLine("during market data streaming using REPLAY mode.");
        Console.WriteLine();
        Console.WriteLine("Benefits of Replay:");
        Console.WriteLine("  ✓ Works anytime (doesn't require market to be open)");
        Console.WriteLine("  ✓ Guaranteed to have data");
        Console.WriteLine("  ✓ Perfect for testing and development");
        Console.WriteLine();

        var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
            ?? throw new InvalidOperationException(
                "DATABENTO_API_KEY environment variable is not set. " +
                "Set it with your API key to authenticate.");

        await RunSymbolResolutionExample(apiKey);

        Console.WriteLine();
        Console.WriteLine("=== Example Complete ===");
    }

    /// <summary>
    /// Demonstrates symbol resolution with replay mode (recommended for testing).
    /// Live mode is shown in comments for reference.
    /// </summary>
    static async Task RunSymbolResolutionExample(string apiKey)
    {
        // ============================================================================
        // STEP 1: Create Symbol Map
        // ============================================================================

        // Symbol map: InstrumentId → Ticker Symbol
        var symbolMap = new ConcurrentDictionary<uint, string>();

        // Performance tracking
        var mappingsReceived = 0;
        var tradesReceived = 0;
        var lookupStopwatch = new Stopwatch();
        long totalLookupTicks = 0;

        // ============================================================================
        // STEP 2: Create Live Client
        // ============================================================================

        await using var client = new LiveClientBuilder()
            .WithApiKey(apiKey)
            .WithDataset("EQUS.MINI")
            .Build();

        Console.WriteLine("✓ Created live client");

        // ============================================================================
        // STEP 3: Subscribe to Data Events
        // ============================================================================

        var firstDataArrived = false;
        var subscriptionStart = Stopwatch.StartNew();

        client.DataReceived += (sender, e) =>
        {
            if (!firstDataArrived)
            {
                firstDataArrived = true;
                Console.WriteLine($"  First record arrived after {subscriptionStart.ElapsedMilliseconds}ms");
                Console.WriteLine();
            }

            // ================================================================
            // CRITICAL: Handle SymbolMappingMessage to build symbol map
            // ================================================================
            if (e.Record is SymbolMappingMessage mapping)
            {
                // ⚠️ IMPORTANT: Use STypeOutSymbol, NOT STypeInSymbol!
                //
                // For ALL_SYMBOLS subscriptions:
                //   STypeInSymbol  = "ALL_SYMBOLS" (your subscription string - same for all)
                //   STypeOutSymbol = "NVDA", "AAPL", etc. (actual ticker - unique)
                //
                // Using STypeInSymbol will make ALL trades show "ALL_SYMBOLS"!

                string symbol = mapping.STypeOutSymbol; // ✓ Correct

                symbolMap[mapping.InstrumentId] = symbol;
                mappingsReceived++;

                // Show first few mappings for demonstration
                if (mappingsReceived <= 5)
                {
                    Console.WriteLine($"  Mapping #{mappingsReceived}:");
                    Console.WriteLine($"    InstrumentId:   {mapping.InstrumentId}");
                    Console.WriteLine($"    STypeInSymbol:  {mapping.STypeInSymbol}");  // Subscription string
                    Console.WriteLine($"    STypeOutSymbol: {mapping.STypeOutSymbol}"); // ✓ Actual ticker
                    Console.WriteLine($"    → Stored: {mapping.InstrumentId} → {symbol}");
                    Console.WriteLine();
                }

                return; // Don't process mapping records as data
            }

            // ================================================================
            // Process data records with symbol resolution
            // ================================================================
            if (e.Record is TradeMessage trade)
            {
                // Measure lookup performance
                lookupStopwatch.Restart();
                var symbol = symbolMap.GetValueOrDefault(
                    trade.InstrumentId,
                    trade.InstrumentId.ToString()); // Fallback to ID if not found
                lookupStopwatch.Stop();
                totalLookupTicks += lookupStopwatch.ElapsedTicks;

                tradesReceived++;

                // Display first few trades
                if (tradesReceived <= 15)
                {
                    var timestamp = trade.Timestamp;
                    Console.WriteLine($"  Trade #{tradesReceived}: {symbol,-6} @ ${trade.PriceDecimal,8:F2} x {trade.Size,5} [{timestamp:HH:mm:ss.fff}]");
                }
                else if (tradesReceived == 16)
                {
                    Console.WriteLine($"  ... (suppressing further output, continuing to collect data)");
                }
            }
        };

        client.ErrorOccurred += (sender, e) =>
        {
            Console.WriteLine($"  [ERROR] {e.Exception.Message}");
        };

        // ============================================================================
        // STEP 4: Subscribe with REPLAY Mode (Most Recent Market Open)
        // ============================================================================

        // Calculate the most recent weekday (Mon-Fri) at 9:30 AM ET for market open
        var now = DateTimeOffset.UtcNow;
        var easternTime = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var nowEastern = TimeZoneInfo.ConvertTime(now, easternTime);

        // Find most recent weekday
        var replayDate = nowEastern.Date;
        while (replayDate.DayOfWeek == DayOfWeek.Saturday || replayDate.DayOfWeek == DayOfWeek.Sunday)
        {
            replayDate = replayDate.AddDays(-1);
        }

        // If current time is before 9:30 AM ET today, use previous weekday
        if (nowEastern.TimeOfDay < TimeSpan.FromHours(9.5))
        {
            replayDate = replayDate.AddDays(-1);
            while (replayDate.DayOfWeek == DayOfWeek.Saturday || replayDate.DayOfWeek == DayOfWeek.Sunday)
            {
                replayDate = replayDate.AddDays(-1);
            }
        }

        // Market open: 9:30 AM ET
        var marketOpen = new DateTimeOffset(
            replayDate.Year, replayDate.Month, replayDate.Day,
            9, 30, 0, // 9:30 AM
            easternTime.GetUtcOffset(replayDate)
        );

        Console.WriteLine($"Subscribing to NVDA, AAPL trades in REPLAY mode...");
        Console.WriteLine($"  Replay Start: {marketOpen:yyyy-MM-dd HH:mm:ss zzz} (Market Open)");
        Console.WriteLine();

        await client.SubscribeAsync(
            dataset: "EQUS.MINI",
            schema: Schema.Trades,
            symbols: new[] { "NVDA", "AAPL" },
            startTime: marketOpen  // ← REPLAY mode: Start from market open
        );

        // ============================================================================
        // ALTERNATIVE: Live Mode (Uncomment to use real-time data)
        // ============================================================================
        /*
        Console.WriteLine("Subscribing to NVDA, AAPL trades in LIVE mode...");
        Console.WriteLine("  Note: This requires the market to be open to receive trades.");
        Console.WriteLine();

        await client.SubscribeAsync(
            dataset: "EQUS.MINI",
            schema: Schema.Trades,
            symbols: new[] { "NVDA", "AAPL" }
            // No 'start' parameter = LIVE mode
        );
        */

        Console.WriteLine("✓ Subscribed");
        Console.WriteLine();
        Console.WriteLine("Starting stream...");

        var metadata = await client.StartAsync();

        Console.WriteLine("✓ Stream started");
        Console.WriteLine();
        Console.WriteLine($"Metadata:");
        Console.WriteLine($"  Dataset:  {metadata.Dataset}");
        Console.WriteLine($"  Symbols:  {string.Join(", ", metadata.Symbols)}");
        Console.WriteLine($"  Schema:   {metadata.Schema}");
        Console.WriteLine();
        Console.WriteLine("Receiving data (will run for 30 seconds of replay data)...");
        Console.WriteLine();

        // Stream for 30 seconds
        var timeout = Task.Delay(TimeSpan.FromSeconds(30));
        var streamTask = Task.Run(async () =>
        {
            await foreach (var record in client.StreamAsync())
            {
                // Records are handled by DataReceived event
                // This just keeps the stream alive
            }
        });

        await Task.WhenAny(streamTask, timeout);

        // Stop streaming
        Console.WriteLine();
        Console.WriteLine("Stopping stream...");
        await client.StopAsync();

        // ============================================================================
        // STEP 5: Performance Report
        // ============================================================================

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("                   Performance Report                  ");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine($"SymbolMappingMessages received: {mappingsReceived}");
        Console.WriteLine($"Trade records received:         {tradesReceived}");
        Console.WriteLine($"Symbol map size:                {symbolMap.Count} mappings");
        Console.WriteLine();

        if (tradesReceived > 0)
        {
            var avgLookupNanos = (totalLookupTicks * 1_000_000_000.0) / (tradesReceived * Stopwatch.Frequency);
            Console.WriteLine($"Symbol Lookup Performance:");
            Console.WriteLine($"  Total lookups:      {tradesReceived}");
            Console.WriteLine($"  Total time:         {totalLookupTicks} ticks");
            Console.WriteLine($"  Average per lookup: {avgLookupNanos:F1} nanoseconds");
            Console.WriteLine($"  Throughput:         {tradesReceived / 30.0:F1} lookups/second");
            Console.WriteLine();
            Console.WriteLine($"Performance Assessment: {(avgLookupNanos < 100 ? "✓ Excellent" : avgLookupNanos < 500 ? "✓ Good" : "⚠️ Review")}");
            Console.WriteLine($"  (Symbol lookup overhead is {avgLookupNanos:F1}ns vs ~1-50ms network latency)");
        }
        else
        {
            Console.WriteLine("⚠️  No trades received.");
            Console.WriteLine("    This may occur if:");
            Console.WriteLine("    - The replay date had no trading activity");
            Console.WriteLine("    - Network connectivity issues");
            Console.WriteLine("    - API subscription limits");
        }
        Console.WriteLine();

        // ============================================================================
        // STEP 6: Validation - Show Symbol Map Contents
        // ============================================================================

        if (symbolMap.Count > 0)
        {
            Console.WriteLine("Symbol Map Contents:");
            Console.WriteLine("─────────────────────");
            foreach (var kvp in symbolMap.OrderBy(x => x.Value))
            {
                Console.WriteLine($"  {kvp.Key,8} → {kvp.Value}");
            }
            Console.WriteLine();
            Console.WriteLine("✓ Symbol resolution working correctly!");
        }
        else
        {
            Console.WriteLine("⚠️  Warning: No symbol mappings received!");
            Console.WriteLine("    Symbol mappings should arrive before data records.");
            Console.WriteLine("    Check subscription parameters and market data availability.");
        }
    }
}
