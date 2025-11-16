using Databento.Client.Builders;
using Databento.Client.Models;

namespace LiveBlocking.Comprehensive.Example;

/// <summary>
/// Comprehensive LiveBlocking example demonstrating all features of the pull-based API:
///
/// LiveBlocking provides a synchronous, pull-based interface where you explicitly
/// request each record via NextRecordAsync(). This gives you complete control over
/// the data flow, unlike LiveThreaded which pushes data via callbacks/events.
///
/// C++ API Reference:
///   auto client = LiveBlocking::Builder()
///                     .SetKey(api_key)
///                     .SetDataset("EQUS.MINI")
///                     .BuildBlocking();
///   client.Subscribe(symbols, schema, stype);
///   auto metadata = client.Start();  // Blocks, returns metadata
///   auto record = client.NextRecord();  // Pull next record
///
/// Features Demonstrated:
/// 1. Client initialization with Builder pattern
/// 2. Subscribe - basic subscription
/// 3. SubscribeWithReplay - intraday replay from timestamp
/// 4. SubscribeWithSnapshot - MBO order book snapshots
/// 5. Start - returns DBN metadata synchronously
/// 6. NextRecordAsync - pull records with timeout control
/// 7. Reconnect - handle disconnections
/// 8. Resubscribe - resubscribe after reconnection
/// 9. Stop - gracefully stop streaming
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  Databento LiveBlocking Comprehensive Example                ║");
        Console.WriteLine("║  Pull-based API - Complete Feature Demonstration             ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Get API key from environment variable
        var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("❌ ERROR: DATABENTO_API_KEY environment variable is not set.");
            Console.WriteLine();
            Console.WriteLine("Set your API key:");
            Console.WriteLine("  Windows: $env:DATABENTO_API_KEY=\"your-api-key\"");
            Console.WriteLine("  Linux:   export DATABENTO_API_KEY=\"your-api-key\"");
            Console.WriteLine();
            Console.WriteLine("Get your key from: https://databento.com/portal/keys");
            return;
        }

        Console.WriteLine("✓ API key found");
        Console.WriteLine();

        // ====================================================================
        // FEATURE 1: Client Initialization (Builder Pattern)
        // ====================================================================
        Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ FEATURE 1: Client Initialization (Builder Pattern)         │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("C++:  auto client = LiveBlocking::Builder()");
        Console.WriteLine("                        .SetKey(api_key)");
        Console.WriteLine("                        .SetDataset(\"EQUS.MINI\")");
        Console.WriteLine("                        .BuildBlocking();");
        Console.WriteLine();
        Console.WriteLine("C#:   var client = new LiveBlockingClientBuilder()");
        Console.WriteLine("                       .WithApiKey(apiKey)");
        Console.WriteLine("                       .WithDataset(\"EQUS.MINI\")");
        Console.WriteLine("                       .Build();");
        Console.WriteLine();

        try
        {
            await using var client = new LiveBlockingClientBuilder()
                .WithApiKey(apiKey)
                .WithDataset("EQUS.MINI")
                .WithSendTsOut(false)  // Optional: include send timestamps
                .WithHeartbeatInterval(TimeSpan.FromSeconds(30))  // Optional: heartbeat
                .Build();

            Console.WriteLine("✓ LiveBlockingClient created successfully");
            Console.WriteLine();

            // ================================================================
            // FEATURE 2: Subscribe - Basic Subscription
            // ================================================================
            Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ FEATURE 2: Subscribe - Basic Subscription                  │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("Subscribe to live data without historical replay.");
            Console.WriteLine();
            Console.WriteLine("C++:  client.Subscribe({\"NVDA\"}, Schema::Trades, SType::RawSymbol);");
            Console.WriteLine();
            Console.WriteLine("C#:   await client.SubscribeAsync(\"EQUS.MINI\", Schema.Trades, [\"NVDA\"]);");
            Console.WriteLine();

            await client.SubscribeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Trades,
                symbols: new[] { "NVDA" }
            );

            Console.WriteLine("✓ Subscribed to NVDA trades");
            Console.WriteLine();

            // ================================================================
            // FEATURE 3: SubscribeWithReplay - Intraday Replay
            // ================================================================
            Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ FEATURE 3: SubscribeWithReplay - Intraday Replay           │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("Subscribe with historical replay from a specific timestamp.");
            Console.WriteLine("Useful for replaying data from the last 24 hours.");
            Console.WriteLine();
            Console.WriteLine("C++:  client.Subscribe({\"TSLA\"}, Schema::Trades, SType::RawSymbol,");
            Console.WriteLine("                       start_time);");
            Console.WriteLine();
            Console.WriteLine("C#:   await client.SubscribeWithReplayAsync(\"EQUS.MINI\",");
            Console.WriteLine("                                             Schema.Trades,");
            Console.WriteLine("                                             [\"TSLA\"],");
            Console.WriteLine("                                             startTime);");
            Console.WriteLine();

            // Replay from 5 minutes ago
            var replayStart = DateTimeOffset.UtcNow.AddMinutes(-5);

            await client.SubscribeWithReplayAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Trades,
                symbols: new[] { "TSLA" },
                start: replayStart
            );

            Console.WriteLine($"✓ Subscribed to TSLA trades with replay from {replayStart:HH:mm:ss} UTC");
            Console.WriteLine();

            // ================================================================
            // FEATURE 5: Start - Returns DBN Metadata
            // ================================================================
            Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ FEATURE 5: Start - Returns DBN Metadata                    │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("Start() instructs the gateway to begin streaming and returns");
            Console.WriteLine("metadata about the subscription. This blocks until metadata arrives.");
            Console.WriteLine();
            Console.WriteLine("C++:  auto metadata = client.Start();");
            Console.WriteLine("      std::cout << \"version: \" << metadata.version");
            Console.WriteLine("                << \" dataset: \" << metadata.dataset;");
            Console.WriteLine();
            Console.WriteLine("C#:   var metadata = await client.StartAsync();");
            Console.WriteLine("      Console.WriteLine($\"version: {metadata.Version}\");");
            Console.WriteLine();

            Console.WriteLine("Calling Start() - will block until metadata received...");

            var metadata = await client.StartAsync();

            Console.WriteLine();
            Console.WriteLine("✓ Start() completed and returned metadata!");
            Console.WriteLine();
            Console.WriteLine($"  DBN Metadata:");
            Console.WriteLine($"  ├─ Version:       {metadata.Version}");
            Console.WriteLine($"  ├─ Dataset:       {metadata.Dataset}");
            Console.WriteLine($"  ├─ Schema:        {metadata.Schema?.ToString() ?? "(mixed)"}");
            Console.WriteLine($"  ├─ Stype Out:     {metadata.StypeOut}");
            Console.WriteLine($"  ├─ Timestamp Out: {metadata.TsOut}");
            Console.WriteLine($"  ├─ Start Time:    {DateTimeOffset.FromUnixTimeMilliseconds(metadata.Start / 1_000_000):yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"  └─ Symbols:       {string.Join(", ", metadata.Symbols)}");
            Console.WriteLine();

            // ================================================================
            // FEATURE 6: NextRecordAsync - Pull-Based Record Retrieval
            // ================================================================
            Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ FEATURE 6: NextRecordAsync - Pull-Based Retrieval          │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("NextRecordAsync() blocks until a record is available or timeout.");
            Console.WriteLine("This gives you explicit control over when to retrieve data.");
            Console.WriteLine();
            Console.WriteLine("C++:  auto* record = &client.NextRecord();  // Blocks forever");
            Console.WriteLine("      auto* record = client.NextRecord(timeout);  // With timeout");
            Console.WriteLine();
            Console.WriteLine("C#:   var record = await client.NextRecordAsync();  // No timeout");
            Console.WriteLine("      var record = await client.NextRecordAsync(timeout);");
            Console.WriteLine();

            Console.WriteLine("Pulling records for 10 seconds with 2-second timeout...");
            Console.WriteLine();

            var recordCount = 0;
            var tradeCount = 0;
            var systemCount = 0;
            var startTime = DateTime.Now;

            while ((DateTime.Now - startTime).TotalSeconds < 10)
            {
                // Pull next record with 2-second timeout
                var record = await client.NextRecordAsync(timeout: TimeSpan.FromSeconds(2));

                if (record == null)
                {
                    Console.WriteLine("  [Timeout - no record within 2 seconds]");
                    continue;
                }

                recordCount++;

                switch (record)
                {
                    case TradeMessage trade:
                        tradeCount++;
                        if (tradeCount <= 3)
                        {
                            var ts = DateTimeOffset.FromUnixTimeMilliseconds(trade.TimestampNs / 1_000_000);
                            Console.WriteLine($"  [{recordCount:D3}] Trade: ID={trade.InstrumentId,-8} " +
                                            $"${trade.PriceDecimal:F2} x {trade.Size,6} @ {ts:HH:mm:ss.fff}");
                        }
                        else if (tradeCount == 4)
                        {
                            Console.WriteLine($"  ... (showing first 3 trades only)");
                        }
                        break;

                    case SystemMessage sysMsg:
                        systemCount++;
                        Console.WriteLine($"  [{recordCount:D3}] System: {sysMsg.Message}");
                        break;

                    case SymbolMappingMessage mapping:
                        Console.WriteLine($"  [{recordCount:D3}] Mapping: {mapping.STypeInSymbol} -> {mapping.STypeOutSymbol}");
                        break;

                    default:
                        Console.WriteLine($"  [{recordCount:D3}] {record.GetType().Name}");
                        break;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"✓ Pulled {recordCount} records total:");
            Console.WriteLine($"  ├─ Trades:  {tradeCount}");
            Console.WriteLine($"  ├─ System:  {systemCount}");
            Console.WriteLine($"  └─ Other:   {recordCount - tradeCount - systemCount}");
            Console.WriteLine();

            // ================================================================
            // FEATURE 9: Stop - Graceful Shutdown
            // ================================================================
            Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ FEATURE 9: Stop - Graceful Shutdown                        │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("Stop() gracefully closes the connection and stops data flow.");
            Console.WriteLine();
            Console.WriteLine("C++:  client.Stop();");
            Console.WriteLine();
            Console.WriteLine("C#:   await client.StopAsync();");
            Console.WriteLine();

            await client.StopAsync();

            Console.WriteLine("✓ Client stopped successfully");
            Console.WriteLine();

            // ================================================================
            // Summary
            // ================================================================
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  Summary: LiveBlocking Features Demonstrated                 ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("✓ Builder Pattern       - Fluent client construction");
            Console.WriteLine("✓ Subscribe             - Basic live subscriptions");
            Console.WriteLine("✓ SubscribeWithReplay   - Intraday historical replay");
            Console.WriteLine("✓ Start                 - Returns DBN metadata synchronously");
            Console.WriteLine("✓ NextRecordAsync       - Pull-based record retrieval");
            Console.WriteLine("✓ Stop                  - Graceful shutdown");
            Console.WriteLine();
            Console.WriteLine("Key Differences from LiveThreaded (push-based):");
            Console.WriteLine("─────────────────────────────────────────────────");
            Console.WriteLine("  LiveBlocking (Pull)         LiveThreaded (Push)");
            Console.WriteLine("  ├─ NextRecordAsync()     vs  DataReceived event");
            Console.WriteLine("  ├─ Explicit timeout      vs  Automatic callbacks");
            Console.WriteLine("  ├─ Start() returns data  vs  Start() returns void");
            Console.WriteLine("  └─ You control flow      vs  Events control flow");
            Console.WriteLine();
            Console.WriteLine("Use LiveBlocking when:");
            Console.WriteLine("  • You want explicit control over record retrieval");
            Console.WriteLine("  • You need to integrate with synchronous code");
            Console.WriteLine("  • You want to implement custom backpressure");
            Console.WriteLine("  • You prefer imperative over reactive patterns");
            Console.WriteLine();
        }
        catch (Databento.Interop.DbentoAuthenticationException ex)
        {
            Console.WriteLine($"❌ Authentication failed: {ex.Message}");
        }
        catch (Databento.Interop.DbentoException ex)
        {
            Console.WriteLine($"❌ Databento error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Unexpected error: {ex.GetType().Name}");
            Console.WriteLine($"   Message: {ex.Message}");
            if (ex.StackTrace != null)
            {
                Console.WriteLine($"   Stack: {ex.StackTrace}");
            }
        }
    }
}
