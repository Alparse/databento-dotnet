using System.Collections.Concurrent;
using System.Diagnostics;
using Databento.Client.Builders;
using Databento.Client.Models;

namespace LiveSymbolResolution.Example;

/// <summary>
/// Demonstrates symbol resolution during live streaming.
/// Shows manual approach and performance characteristics.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Live Symbol Resolution Example ===");
        Console.WriteLine();
        Console.WriteLine("This example demonstrates how to resolve InstrumentId → Ticker Symbol");
        Console.WriteLine("during live market data streaming.");
        Console.WriteLine();

        var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
            ?? throw new InvalidOperationException(
                "DATABENTO_API_KEY environment variable is not set. " +
                "Set it with your API key to authenticate.");

        // ============================================================================
        // Approach 1: Manual ConcurrentDictionary (Current Recommended Method)
        // ============================================================================

        Console.WriteLine("Approach 1: Manual Symbol Resolution");
        Console.WriteLine("─────────────────────────────────────");
        Console.WriteLine("Using ConcurrentDictionary<uint, string> to build symbol map manually.");
        Console.WriteLine();

        await RunManualApproach(apiKey);

        Console.WriteLine();
        Console.WriteLine("=== Example Complete ===");
    }

    /// <summary>
    /// Manual approach: Build symbol map using ConcurrentDictionary
    /// </summary>
    static async Task RunManualApproach(string apiKey)
    {
        // Symbol map: InstrumentId → Ticker Symbol
        var symbolMap = new ConcurrentDictionary<uint, string>();

        // Performance tracking
        var mappingsReceived = 0;
        var tradesReceived = 0;
        var lookupStopwatch = new Stopwatch();
        long totalLookupTicks = 0;

        // Create live client
        await using var client = new LiveClientBuilder()
            .WithApiKey(apiKey)
            .WithDataset("EQUS.MINI")
            .Build();

        Console.WriteLine("✓ Created live client");

        // Track when first data arrives
        var firstDataArrived = false;
        var subscriptionStart = Stopwatch.StartNew();

        // Subscribe to data received events
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
                // IMPORTANT: Use STypeOutSymbol, NOT STypeInSymbol!
                // For ALL_SYMBOLS subscriptions, STypeInSymbol = "ALL_SYMBOLS" (useless)
                // STypeOutSymbol = "NVDA", "AAPL", etc. (actual ticker symbols)
                string symbol = mapping.STypeOutSymbol;

                symbolMap[mapping.InstrumentId] = symbol;
                mappingsReceived++;

                // Show first few mappings for demonstration
                if (mappingsReceived <= 5)
                {
                    Console.WriteLine($"  Mapping #{mappingsReceived}:");
                    Console.WriteLine($"    InstrumentId:  {mapping.InstrumentId}");
                    Console.WriteLine($"    STypeInSymbol:  {mapping.STypeInSymbol}");  // Subscription string
                    Console.WriteLine($"    STypeOutSymbol: {mapping.STypeOutSymbol}"); // Actual ticker ✓
                    Console.WriteLine($"    → Using: {symbol}");
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
                    trade.InstrumentId.ToString());
                lookupStopwatch.Stop();
                totalLookupTicks += lookupStopwatch.ElapsedTicks;

                tradesReceived++;

                // Display first few trades
                if (tradesReceived <= 10)
                {
                    Console.WriteLine($"  Trade #{tradesReceived}: {symbol} @ ${trade.PriceDecimal:F2} x {trade.Size}");
                }
                else if (tradesReceived == 11)
                {
                    Console.WriteLine($"  ... (suppressing further trade output)");
                }
            }
        };

        // Subscribe to errors
        client.ErrorOccurred += (sender, e) =>
        {
            Console.WriteLine($"  [ERROR] {e.Exception.Message}");
        };

        // ================================================================
        // Subscribe and Start
        // ================================================================

        Console.WriteLine("Subscribing to NVDA, AAPL trades...");
        await client.SubscribeAsync(
            dataset: "EQUS.MINI",
            schema: Schema.Trades,
            symbols: new[] { "NVDA", "AAPL" }
        );

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
        Console.WriteLine("Receiving data (will run for 10 seconds)...");
        Console.WriteLine();

        // Stream for 10 seconds
        var timeout = Task.Delay(TimeSpan.FromSeconds(10));
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
        await client.StopAsync();

        // ================================================================
        // Performance Report
        // ================================================================

        Console.WriteLine();
        Console.WriteLine("Performance Report");
        Console.WriteLine("══════════════════");
        Console.WriteLine($"SymbolMappingMessages received: {mappingsReceived}");
        Console.WriteLine($"Trade records received:         {tradesReceived}");
        Console.WriteLine($"Symbol map size:                {symbolMap.Count} mappings");
        Console.WriteLine();

        if (tradesReceived > 0)
        {
            var avgLookupNanos = (totalLookupTicks * 1_000_000_000.0) / (tradesReceived * Stopwatch.Frequency);
            Console.WriteLine($"Symbol Lookup Performance:");
            Console.WriteLine($"  Total lookups:     {tradesReceived}");
            Console.WriteLine($"  Total time:        {totalLookupTicks} ticks");
            Console.WriteLine($"  Average per lookup: {avgLookupNanos:F1} nanoseconds");
            Console.WriteLine($"  Throughput:        {tradesReceived / 10.0:F1} lookups/second");
        }
        Console.WriteLine();

        // ================================================================
        // Validation: Show what's in the symbol map
        // ================================================================

        if (symbolMap.Count > 0)
        {
            Console.WriteLine("Symbol Map Contents:");
            foreach (var kvp in symbolMap.OrderBy(x => x.Value))
            {
                Console.WriteLine($"  {kvp.Key,8} → {kvp.Value}");
            }
        }
        else
        {
            Console.WriteLine("⚠️  Warning: No symbol mappings received!");
            Console.WriteLine("    This may indicate the market is closed or symbols are not actively trading.");
        }
    }

    // ============================================================================
    // NOTE: PitSymbolMap Approach (Future)
    // ============================================================================

    /*
     * Once PitSymbolMap.CreateEmpty() is available, you could use:
     *
     * using var pitMap = PitSymbolMap.CreateEmpty();
     *
     * client.DataReceived += (sender, e) =>
     * {
     *     if (e.Record is SymbolMappingMessage mapping)
     *     {
     *         pitMap.OnSymbolMapping(mapping);
     *         return;
     *     }
     *
     *     if (e.Record is TradeMessage trade)
     *     {
     *         var symbol = pitMap.Find(trade) ?? trade.InstrumentId.ToString();
     *         Console.WriteLine($"{symbol}: ${trade.PriceDecimal}");
     *     }
     * };
     *
     * Performance comparison:
     * - Manual ConcurrentDictionary: Faster lookups (pure managed, ~20-50ns per lookup)
     * - PitSymbolMap: Slower lookups (P/Invoke overhead, ~100-200ns per lookup)
     *
     * Trade-offs:
     * - Manual: Best performance, more boilerplate
     * - PitSymbolMap: Cleaner API, consistent with databento-cpp, slight overhead
     *
     * For most applications, the overhead is negligible compared to network I/O.
     * For ultra-low-latency HFT, manual approach may be preferred.
     */
}
