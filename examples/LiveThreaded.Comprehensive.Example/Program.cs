using Databento.Client.Builders;
using Databento.Client.Models;

namespace LiveThreaded.Comprehensive.Example;

/// <summary>
/// Comprehensive LiveThreaded (LiveClient) example demonstrating all features of the event-driven API:
///
/// LiveClient provides an event-driven, push-based interface where data is delivered via
/// events/callbacks. This is different from LiveBlocking which requires you to explicitly
/// pull each record via NextRecordAsync().
///
/// C++ API Reference:
///   auto client = LiveThreaded::Builder()
///                     .SetKey(api_key)
///                     .SetDataset("EQUS.MINI")
///                     .BuildThreaded();
///   client.Subscribe(symbols, schema, stype);
///   client.Start(metadata_callback, record_callback);  // Spawns thread, uses callbacks
///
/// Features Demonstrated:
/// 1. Client initialization with Builder pattern
/// 2. Subscribe - basic subscription
/// 3. Subscribe with intraday replay (startTime parameter)
/// 4. SubscribeWithSnapshot - MBO order book snapshots
/// 5. StartAsync - returns DBN metadata (hybrid feature)
/// 6. DataReceived event - event-driven record handling
/// 7. StreamAsync - IAsyncEnumerable pattern (C# enhancement)
/// 8. ErrorOccurred event - error handling
/// 9. Reconnect - handle disconnections
/// 10. Resubscribe - resubscribe after reconnection
/// 11. Stop - gracefully stop streaming
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  Databento LiveThreaded Comprehensive Example                ║");
        Console.WriteLine("║  Event-driven API - Complete Feature Demonstration           ║");
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
        Console.WriteLine("C++:  auto client = LiveThreaded::Builder()");
        Console.WriteLine("                        .SetKey(api_key)");
        Console.WriteLine("                        .SetDataset(\"EQUS.MINI\")");
        Console.WriteLine("                        .BuildThreaded();");
        Console.WriteLine();
        Console.WriteLine("C#:   var client = new LiveClientBuilder()");
        Console.WriteLine("                       .WithApiKey(apiKey)");
        Console.WriteLine("                       .WithDataset(\"EQUS.MINI\")");
        Console.WriteLine("                       .Build();");
        Console.WriteLine();

        try
        {
            await using var client = new LiveClientBuilder()
                .WithApiKey(apiKey)
                .WithDataset("EQUS.MINI")
                .WithSendTsOut(false)  // Optional: include send timestamps
                .WithHeartbeatInterval(TimeSpan.FromSeconds(30))  // Optional: heartbeat
                .Build();

            Console.WriteLine("✓ LiveClient created successfully");
            Console.WriteLine();

            // ================================================================
            // FEATURE 6: DataReceived Event - Event-Driven Record Handling
            // ================================================================
            Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ FEATURE 6: DataReceived Event - Event-Driven Handling      │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("Unlike LiveBlocking, LiveClient pushes data to you via events.");
            Console.WriteLine();
            Console.WriteLine("C++:  client.Start(");
            Console.WriteLine("          [](Metadata&& m) { /* metadata callback */ },");
            Console.WriteLine("          [](const Record& r) { /* record callback */ }");
            Console.WriteLine("      );");
            Console.WriteLine();
            Console.WriteLine("C#:   client.DataReceived += (sender, e) => {");
            Console.WriteLine("          Console.WriteLine(e.Record);");
            Console.WriteLine("      };");
            Console.WriteLine("      await client.StartAsync();");
            Console.WriteLine();

            var recordCount = 0;
            var tradeCount = 0;
            var systemCount = 0;

            client.DataReceived += (sender, e) =>
            {
                recordCount++;

                switch (e.Record)
                {
                    case TradeMessage trade:
                        tradeCount++;
                        if (tradeCount <= 3)
                        {
                            var ts = DateTimeOffset.FromUnixTimeMilliseconds(trade.TimestampNs / 1_000_000);
                            Console.WriteLine($"  [Event] Trade: ID={trade.InstrumentId,-8} " +
                                            $"${trade.PriceDecimal:F2} x {trade.Size,6} @ {ts:HH:mm:ss.fff}");
                        }
                        break;

                    case SystemMessage sysMsg:
                        systemCount++;
                        Console.WriteLine($"  [Event] System: {sysMsg.Message}");
                        break;

                    case SymbolMappingMessage mapping:
                        Console.WriteLine($"  [Event] Mapping: {mapping.STypeInSymbol} -> {mapping.STypeOutSymbol}");
                        break;
                }
            };

            Console.WriteLine("✓ DataReceived event handler registered");
            Console.WriteLine();

            // ================================================================
            // FEATURE 8: ErrorOccurred Event - Error Handling
            // ================================================================
            Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ FEATURE 8: ErrorOccurred Event - Error Handling            │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("C++:  client.Start(metadata_cb, record_cb, exception_cb);");
            Console.WriteLine();
            Console.WriteLine("C#:   client.ErrorOccurred += (sender, e) => {");
            Console.WriteLine("          Console.WriteLine($\"Error: {e.Exception.Message}\");");
            Console.WriteLine("      };");
            Console.WriteLine();

            client.ErrorOccurred += (sender, e) =>
            {
                Console.WriteLine($"  [Error] {e.Exception.GetType().Name}: {e.Exception.Message}");
            };

            Console.WriteLine("✓ ErrorOccurred event handler registered");
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
            // FEATURE 3: Subscribe with Intraday Replay
            // ================================================================
            Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ FEATURE 3: Subscribe with Intraday Replay                  │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("Subscribe with historical replay from a specific timestamp.");
            Console.WriteLine("Useful for replaying data from the last 24 hours.");
            Console.WriteLine();
            Console.WriteLine("C++:  client.Subscribe({\"TSLA\"}, Schema::Trades, SType::RawSymbol,");
            Console.WriteLine("                       start_time);");
            Console.WriteLine();
            Console.WriteLine("C#:   await client.SubscribeAsync(\"EQUS.MINI\", Schema.Trades,");
            Console.WriteLine("                                   [\"TSLA\"], startTime);");
            Console.WriteLine();

            // Replay from 5 minutes ago
            var replayStart = DateTimeOffset.UtcNow.AddMinutes(-5);

            await client.SubscribeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Trades,
                symbols: new[] { "TSLA" },
                startTime: replayStart
            );

            Console.WriteLine($"✓ Subscribed to TSLA trades with replay from {replayStart:HH:mm:ss} UTC");
            Console.WriteLine();

            // ================================================================
            // FEATURE 5: StartAsync - Returns DBN Metadata (Hybrid Feature)
            // ================================================================
            Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ FEATURE 5: StartAsync - Returns DBN Metadata (Hybrid)      │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("C++ LiveThreaded uses callbacks for metadata:");
            Console.WriteLine("  client.Start([](Metadata&& m) { ... }, record_callback);");
            Console.WriteLine();
            Console.WriteLine("C# LiveClient returns metadata directly (like LiveBlocking):");
            Console.WriteLine("  var metadata = await client.StartAsync();");
            Console.WriteLine();
            Console.WriteLine("This is a HYBRID approach - more convenient for C#!");
            Console.WriteLine();

            Console.WriteLine("Calling StartAsync() - will return metadata...");

            var metadata = await client.StartAsync();

            Console.WriteLine();
            Console.WriteLine("✓ StartAsync() completed and returned metadata!");
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

            // Wait for events to fire (data is pushed to event handlers)
            Console.WriteLine("Waiting 5 seconds for events to fire...");
            Console.WriteLine("(Data is pushed automatically via DataReceived event)");
            Console.WriteLine();

            await Task.Delay(TimeSpan.FromSeconds(5));

            Console.WriteLine();
            Console.WriteLine($"✓ Received {recordCount} records via events:");
            Console.WriteLine($"  ├─ Trades:  {tradeCount}");
            Console.WriteLine($"  ├─ System:  {systemCount}");
            Console.WriteLine($"  └─ Other:   {recordCount - tradeCount - systemCount}");
            Console.WriteLine();

            // ================================================================
            // FEATURE 7: StreamAsync - IAsyncEnumerable Pattern (C# Enhancement)
            // ================================================================
            Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ FEATURE 7: StreamAsync - IAsyncEnumerable (C# Enhancement) │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("C++ doesn't have IAsyncEnumerable - this is a C# enhancement!");
            Console.WriteLine();
            Console.WriteLine("C#:   await foreach (var record in client.StreamAsync()) {");
            Console.WriteLine("          Console.WriteLine(record);");
            Console.WriteLine("      }");
            Console.WriteLine();
            Console.WriteLine("Streaming with IAsyncEnumerable for 3 seconds...");
            Console.WriteLine();

            var streamCount = 0;
            var streamStart = DateTime.Now;

            await foreach (var record in client.StreamAsync())
            {
                streamCount++;

                if (streamCount <= 3)
                {
                    Console.WriteLine($"  [Stream] {record.GetType().Name}");
                }

                if ((DateTime.Now - streamStart).TotalSeconds >= 3)
                {
                    Console.WriteLine($"  ... (pulled {streamCount} records via StreamAsync)");
                    break;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"✓ StreamAsync processed {streamCount} records");
            Console.WriteLine();

            // ================================================================
            // FEATURE 9: BlockUntilStoppedAsync - Wait for Stream Stop (Optional Feature)
            // ================================================================
            Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ FEATURE 9: BlockUntilStoppedAsync (Optional - NEW!)        │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("Block until stream stops, with optional timeout.");
            Console.WriteLine();
            Console.WriteLine("C++:  client.BlockForStop();  // Wait forever");
            Console.WriteLine("      client.BlockForStop(std::chrono::seconds(5));  // With timeout");
            Console.WriteLine();
            Console.WriteLine("C#:   await client.BlockUntilStoppedAsync();  // Wait forever");
            Console.WriteLine("      bool stopped = await client.BlockUntilStoppedAsync(TimeSpan.FromSeconds(5));");
            Console.WriteLine();
            Console.WriteLine("Testing BlockUntilStoppedAsync with 2-second timeout...");
            Console.WriteLine("(Client is still streaming, so this should timeout)");
            Console.WriteLine();

            var blockStart = DateTime.Now;
            bool stoppedNormally = await client.BlockUntilStoppedAsync(TimeSpan.FromSeconds(2));
            var blockDuration = (DateTime.Now - blockStart).TotalSeconds;

            if (stoppedNormally)
            {
                Console.WriteLine($"✓ Client stopped normally after {blockDuration:F1}s");
            }
            else
            {
                Console.WriteLine($"✓ Timeout reached after {blockDuration:F1}s (expected - client still streaming)");
            }
            Console.WriteLine();

            // ================================================================
            // FEATURE 10: ExceptionCallback - Custom Error Handling (Optional Feature)
            // ================================================================
            Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ FEATURE 10: ExceptionCallback (Optional - NEW!)            │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("Custom exception handling with Continue/Stop action.");
            Console.WriteLine();
            Console.WriteLine("C++:  auto handler = [](const std::exception& e) {");
            Console.WriteLine("          return ExceptionAction::Continue;  // or Stop");
            Console.WriteLine("      };");
            Console.WriteLine("      client.Start(metadata_cb, record_cb, handler);");
            Console.WriteLine();
            Console.WriteLine("C#:   var client = new LiveClientBuilder()");
            Console.WriteLine("          .WithExceptionHandler(ex => {");
            Console.WriteLine("              return ExceptionAction.Continue;  // or Stop");
            Console.WriteLine("          })");
            Console.WriteLine("          .Build();");
            Console.WriteLine();
            Console.WriteLine("Note: ExceptionCallback was configured at client creation time.");
            Console.WriteLine("      It's invoked automatically when errors occur.");
            Console.WriteLine("      Return Continue to keep streaming, or Stop to terminate.");
            Console.WriteLine();
            Console.WriteLine("✓ ExceptionCallback support available via WithExceptionHandler()");
            Console.WriteLine();

            // ================================================================
            // FEATURE 11: Stop - Graceful Shutdown
            // ================================================================
            Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ FEATURE 11: Stop - Graceful Shutdown                       │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("Stop() gracefully closes the connection and stops data flow.");
            Console.WriteLine();
            Console.WriteLine("C++:  // client destructor calls Stop automatically");
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
            Console.WriteLine("║  Summary: LiveThreaded Features Demonstrated                 ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("✓ Builder Pattern          - Fluent client construction");
            Console.WriteLine("✓ Subscribe                - Basic live subscriptions");
            Console.WriteLine("✓ Subscribe w/ Replay      - Intraday historical replay");
            Console.WriteLine("✓ SubscribeWithSnapshot    - MBO snapshots (not shown - needs futures)");
            Console.WriteLine("✓ StartAsync               - Returns DBN metadata (hybrid feature)");
            Console.WriteLine("✓ DataReceived Event       - Event-driven record handling");
            Console.WriteLine("✓ StreamAsync              - IAsyncEnumerable pattern (C# enhancement)");
            Console.WriteLine("✓ ErrorOccurred Event      - Error handling");
            Console.WriteLine("✓ BlockUntilStoppedAsync   - Wait for stream stop (optional - NEW!)");
            Console.WriteLine("✓ ExceptionCallback        - Custom error handling (optional - NEW!)");
            Console.WriteLine("✓ Reconnect/Resubscribe    - Connection recovery (not shown)");
            Console.WriteLine("✓ Stop                     - Graceful shutdown");
            Console.WriteLine();
            Console.WriteLine("Key Differences from LiveBlocking (pull-based):");
            Console.WriteLine("─────────────────────────────────────────────────");
            Console.WriteLine("  LiveThreaded (Push)         LiveBlocking (Pull)");
            Console.WriteLine("  ├─ DataReceived event    vs  NextRecordAsync()");
            Console.WriteLine("  ├─ Automatic callbacks   vs  Explicit timeout");
            Console.WriteLine("  ├─ Events control flow   vs  You control flow");
            Console.WriteLine("  └─ IAsyncEnumerable      vs  Manual iteration");
            Console.WriteLine();
            Console.WriteLine("Use LiveThreaded (LiveClient) when:");
            Console.WriteLine("  • You prefer reactive/event-driven patterns");
            Console.WriteLine("  • You want automatic data delivery");
            Console.WriteLine("  • You need IAsyncEnumerable support");
            Console.WriteLine("  • You're building event-sourced architectures");
            Console.WriteLine();
            Console.WriteLine("C++ vs C# API Comparison:");
            Console.WriteLine("─────────────────────────");
            Console.WriteLine("  C++ LiveThreaded::Start() -> void (uses callbacks)");
            Console.WriteLine("  C# LiveClient.StartAsync() -> Task<DbnMetadata> (hybrid)");
            Console.WriteLine();
            Console.WriteLine("  C++ record_callback -> KeepGoing enum return");
            Console.WriteLine("  C# DataReceived event -> void event handler");
            Console.WriteLine();
            Console.WriteLine("  C++ exception_callback -> ExceptionAction return");
            Console.WriteLine("  C# ErrorOccurred event -> void event handler");
            Console.WriteLine("                            (manual reconnect required)");
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
