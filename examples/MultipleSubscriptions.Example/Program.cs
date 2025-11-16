using Databento.Client.Builders;
using Databento.Client.Models;

namespace MultipleSubscriptions.Example;

/// <summary>
/// Demonstrates multiple subscriptions matching the C++ example:
///
/// client.Subscribe({"ES.v.0"}, Schema::Trades, SType::Continuous);
/// client.Subscribe({"ES.v.0"}, Schema::Definition, SType::Continuous);
///
/// Multiple subscriptions enable rich data streams containing mixed record types.
/// This example subscribes to both Trades and Definition schemas for NVDA,
/// allowing you to receive both trade data and instrument definitions in one stream.
///
/// Key points:
/// - Supports multiple subscriptions for different schemas
/// - Optional start time for intraday replay (before starting the session)
/// - No unsubscribe method - subscriptions end when client disconnects or Stop() is called
/// - Takes up to 2,000 symbols per request
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Databento Multiple Subscriptions Example");
        Console.WriteLine("========================================\n");

        // Get API key from environment variable
        var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("ERROR: DATABENTO_API_KEY environment variable is not set.");
            Console.WriteLine("Please set your API key and try again.");
            return;
        }

        Console.WriteLine("✓ API key found");
        Console.WriteLine();

        try
        {
            // Create live client
            await using var client = new LiveClientBuilder()
                .WithApiKey(apiKey)
                .WithDataset("EQUS.MINI")
                .Build();

            Console.WriteLine("✓ Created LiveClient with dataset: EQUS.MINI");
            Console.WriteLine();

            // Track different message types
            var recordCount = 0;
            var tradeCount = 0;
            var definitionCount = 0;
            var statusCount = 0;
            var systemCount = 0;

            // Subscribe to data events
            client.DataReceived += (sender, e) =>
            {
                recordCount++;

                // Handle different message types from multiple subscriptions
                switch (e.Record)
                {
                    case TradeMessage trade:
                        tradeCount++;
                        if (tradeCount <= 3)
                        {
                            var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(trade.TimestampNs / 1_000_000);
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Trade #{tradeCount}:");
                            Console.WriteLine($"  Symbol ID: {trade.InstrumentId}");
                            Console.WriteLine($"  Price:     ${trade.PriceDecimal:F2}");
                            Console.WriteLine($"  Size:      {trade.Size}");
                            Console.WriteLine($"  Time:      {timestamp:HH:mm:ss.fff} UTC");
                            Console.WriteLine();
                        }
                        break;

                    case InstrumentDefMessage def:
                        definitionCount++;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] InstrumentDef #{definitionCount}:");
                        Console.WriteLine($"  Raw Symbol:    {def.RawSymbol}");
                        Console.WriteLine($"  Instrument ID: {def.InstrumentId}");
                        Console.WriteLine($"  Exchange:      {def.Exchange}");
                        Console.WriteLine($"  Security Type: {def.SecurityType}");
                        Console.WriteLine($"  Currency:      {def.Currency}");
                        Console.WriteLine();
                        break;

                    case StatusMessage status:
                        statusCount++;
                        if (statusCount <= 5)
                        {
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Status #{statusCount}:");
                            Console.WriteLine($"  Instrument ID: {status.InstrumentId}");
                            Console.WriteLine($"  Action:        {status.Action}");
                            Console.WriteLine($"  Reason:        {status.Reason}");
                            Console.WriteLine();
                        }
                        break;

                    case SystemMessage sysMsg:
                        systemCount++;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {sysMsg}");
                        Console.WriteLine();
                        break;

                    case SymbolMappingMessage symMap:
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] SymbolMapping: {symMap}");
                        Console.WriteLine();
                        break;
                }
            };

            // ===================================================================
            // MULTIPLE SUBSCRIPTIONS - This is the key feature!
            // Subscribe to different schemas for the same symbol
            // This matches the C++ pattern:
            //   client.Subscribe({"ES.v.0"}, Schema::Trades, SType::Continuous);
            //   client.Subscribe({"ES.v.0"}, Schema::Definition, SType::Continuous);
            // ===================================================================

            Console.WriteLine("Creating multiple subscriptions for NVDA:");
            Console.WriteLine("  1. Trades schema     - Real-time trade data");
            Console.WriteLine("  2. Definition schema - Instrument definitions");
            Console.WriteLine("  3. Status schema     - Trading status updates");
            Console.WriteLine();

            // Subscription 1: Trades
            await client.SubscribeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Trades,
                symbols: new[] { "NVDA" }
                // No startTime = real-time only (no replay)
            );
            Console.WriteLine("✓ Subscription 1: NVDA Trades (real-time)");

            // Subscription 2: Instrument Definitions
            await client.SubscribeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Definition,
                symbols: new[] { "NVDA" }
            );
            Console.WriteLine("✓ Subscription 2: NVDA Definitions");

            // Subscription 3: Trading Status
            await client.SubscribeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Status,
                symbols: new[] { "NVDA" }
            );
            Console.WriteLine("✓ Subscription 3: NVDA Trading Status");
            Console.WriteLine();

            Console.WriteLine("All subscriptions created successfully!");
            Console.WriteLine("The data stream will now contain mixed record types:");
            Console.WriteLine("  • TradeMessage");
            Console.WriteLine("  • InstrumentDefMessage");
            Console.WriteLine("  • StatusMessage");
            Console.WriteLine();

            // Start streaming
            Console.WriteLine("Starting live stream...");
            Console.WriteLine("(Receiving mixed record types for 10 seconds)");
            Console.WriteLine();

            _ = await client.StartAsync();

            // Wait 10 seconds
            await Task.Delay(10000);

            // Stop streaming
            try
            {
                await client.StopAsync();
            }
            catch (System.Threading.Channels.ChannelClosedException)
            {
                // Expected during shutdown
            }

            Console.WriteLine();
            Console.WriteLine("Multiple subscriptions example completed successfully!");
            Console.WriteLine();
            Console.WriteLine("Summary:");
            Console.WriteLine($"  Total records received:     {recordCount}");
            Console.WriteLine($"  TradeMessage:               {tradeCount}");
            Console.WriteLine($"  InstrumentDefMessage:       {definitionCount}");
            Console.WriteLine($"  StatusMessage:              {statusCount}");
            Console.WriteLine($"  SystemMessage:              {systemCount}");
            Console.WriteLine($"  Other:                      {recordCount - tradeCount - definitionCount - statusCount - systemCount}");
            Console.WriteLine();

            Console.WriteLine("Key Features Demonstrated:");
            Console.WriteLine("---------------------------");
            Console.WriteLine("✓ Multiple subscriptions for different schemas");
            Console.WriteLine("✓ Mixed record types in a single data stream");
            Console.WriteLine("✓ Same symbol (NVDA) across multiple schemas");
            Console.WriteLine("✓ No unsubscribe method - ended by Stop()");
            Console.WriteLine();

            Console.WriteLine("Example with Intraday Replay (commented out):");
            Console.WriteLine("// Subscribe with replay from 1 hour ago:");
            Console.WriteLine("// var oneHourAgo = DateTimeOffset.UtcNow.AddHours(-1);");
            Console.WriteLine("// await client.SubscribeAsync(");
            Console.WriteLine("//     dataset: \"EQUS.MINI\",");
            Console.WriteLine("//     schema: Schema.Trades,");
            Console.WriteLine("//     symbols: new[] { \"NVDA\" },");
            Console.WriteLine("//     startTime: oneHourAgo  // Intraday replay parameter");
            Console.WriteLine("// );");
            Console.WriteLine();

            Console.WriteLine("Notes:");
            Console.WriteLine("------");
            Console.WriteLine("• Subscriptions support up to 2,000 symbols per request");
            Console.WriteLine("• Optional start time enables intraday replay (last 24 hours)");
            Console.WriteLine("• Start time filters on ts_event (except CBBO/BBO which use ts_recv)");
            Console.WriteLine("• Pass DateTimeOffset.MinValue to request all available replay data");
            Console.WriteLine("• No unsubscribe method - subscriptions end when client disconnects");
            Console.WriteLine("• Multiple subscriptions enable rich, mixed data streams");
        }
        catch (Databento.Interop.DbentoAuthenticationException ex)
        {
            Console.WriteLine($"✗ Authentication failed: {ex.Message}");
        }
        catch (System.Threading.Channels.ChannelClosedException)
        {
            // Expected during cleanup
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.GetType().Name}");
            Console.WriteLine($"  Message: {ex.Message}");
        }
    }
}
