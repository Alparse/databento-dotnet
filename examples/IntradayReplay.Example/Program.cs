using Databento.Client.Builders;
using Databento.Client.Models;

namespace IntradayReplay.Example;

/// <summary>
/// Demonstrates intraday replay functionality matching the C++ example:
/// client.Subscribe({"ES.v.0"}, Schema::Trades, SType::Continuous, UnixNanos{});
///
/// Intraday replay allows you to replay data from within the last 24 hours.
/// Data is filtered on ts_event for all schemas except CBBO/BBO (filtered on ts_recv).
/// A "replay completed" SystemMsg will be sent when caught up to real-time data.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Databento Intraday Replay Example");
        Console.WriteLine("==================================\n");

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
                .WithDataset("EQUS.MINI")  // Using EQUS.MINI as requested
                .Build();

            Console.WriteLine("✓ Created LiveClient with dataset: EQUS.MINI");
            Console.WriteLine();

            // Track record counts
            var recordCount = 0;
            var tradeCount = 0;
            var replayCompleteReceived = false;

            // Subscribe to data events
            client.DataReceived += (sender, e) =>
            {
                recordCount++;

                // Check for replay completed system message
                if (e.Record is SystemMessage sysMsg)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] SystemMessage: {sysMsg}");

                    // Check if this is a "replay completed" message
                    // The message will say "Finished trades replay" or similar
                    var msgStr = sysMsg.ToString();
                    if (msgStr.Contains("Finished", StringComparison.OrdinalIgnoreCase) &&
                        msgStr.Contains("replay", StringComparison.OrdinalIgnoreCase))
                    {
                        replayCompleteReceived = true;
                        Console.WriteLine("✓ Intraday replay completed - now receiving real-time data");
                        Console.WriteLine();
                    }
                }
                else if (e.Record is TradeMessage trade)
                {
                    tradeCount++;

                    // Decode and display first 10 trades and every 100th trade
                    if (tradeCount <= 10 || (tradeCount % 100 == 0))
                    {
                        // Convert timestamp from nanoseconds to DateTime
                        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(trade.TimestampNs / 1_000_000);

                        // Display decoded trade message
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Trade #{tradeCount}:");
                        Console.WriteLine($"  Timestamp:     {timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC");
                        Console.WriteLine($"  Instrument ID: {trade.InstrumentId}");
                        Console.WriteLine($"  Price:         ${trade.PriceDecimal:F2}");
                        Console.WriteLine($"  Size:          {trade.Size}");
                        Console.WriteLine($"  Side:          {trade.Side}");
                        Console.WriteLine($"  Action:        {trade.Action}");
                        Console.WriteLine($"  Flags:         {trade.Flags}");
                    }
                }
                else if (e.Record is SymbolMappingMessage symMap)
                {
                    // Show symbol mappings
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] SymbolMapping: {symMap}");
                }
                else if (recordCount % 1000 == 0)
                {
                    // Show progress every 1000 records
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Progress: {recordCount} total records, {tradeCount} trades");
                }
            };

            // Example 1: Full replay history (DateTimeOffset.MinValue = 0 nanoseconds)
            // This matches databento-cpp: client.Subscribe({"NVDA"}, Schema::Trades, SType::RawSymbol, UnixNanos{});
            Console.WriteLine("Example 1: Subscribing with FULL replay history");
            Console.WriteLine("This replays all available data from the last 24 hours (or weekly session for GLBX.MDP3)");
            Console.WriteLine();

            await client.SubscribeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Trades,
                symbols: new[] { "NVDA" },
                startTime: DateTimeOffset.MinValue  // Full replay history (equivalent to UnixNanos{} in C++)
            );

            Console.WriteLine("✓ Subscribed to NVDA trades with full replay history");
            Console.WriteLine("  Symbol: NVDA");
            Console.WriteLine("  Schema: Trades");
            Console.WriteLine("  Replay: Full history (last 24 hours)");
            Console.WriteLine();

            // Start streaming
            Console.WriteLine("Starting live stream with replay...");
            Console.WriteLine("(Receiving records, will stop after replay completes or 30 seconds)");
            Console.WriteLine();

            await client.StartAsync();

            // Wait for replay to complete or timeout
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromSeconds(30);

            while (DateTime.Now - startTime < timeout)
            {
                await Task.Delay(100);

                // Stop if we received replay completed message
                if (replayCompleteReceived)
                {
                    Console.WriteLine($"Received {recordCount} total records (including replayed data)");
                    break;
                }
            }

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
            Console.WriteLine("Intraday replay example completed successfully!");
            Console.WriteLine();
            Console.WriteLine("Summary:");
            Console.WriteLine($"  Total records received:  {recordCount}");
            Console.WriteLine($"  Trade messages decoded:  {tradeCount}");
            Console.WriteLine($"  Other messages:          {recordCount - tradeCount}");
            Console.WriteLine($"  Replay completed signal: {(replayCompleteReceived ? "Yes" : "No (timeout)")}");
            Console.WriteLine();
            Console.WriteLine("TradeMessage Fields Decoded:");
            Console.WriteLine("  - Timestamp (ts_event):  Nanosecond precision timestamp of the trade");
            Console.WriteLine("  - Instrument ID:         Numeric identifier for NVDA");
            Console.WriteLine("  - Price:                 Trade price converted from fixed-point to decimal");
            Console.WriteLine("  - Size:                  Number of shares traded");
            Console.WriteLine("  - Side:                  Buy/Sell side (Ask='A', Bid='B', None='N')");
            Console.WriteLine("  - Action:                Trade action (Trade, Fill, etc.)");
            Console.WriteLine("  - Flags:                 Bit flags (Last, Snapshot, etc.)");
            Console.WriteLine();
            Console.WriteLine("Notes:");
            Console.WriteLine("  - Intraday replay provides data from the last 24 hours");
            Console.WriteLine("  - Data is filtered on ts_event for trades schema");
            Console.WriteLine("  - A SystemMessage indicates when replay is complete");
            Console.WriteLine("  - After replay, you receive real-time data");
            Console.WriteLine("  - Different start times can be specified for each subscription");
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

        Console.WriteLine();
        Console.WriteLine("Example 2 (commented out): Replay from a specific time");
        Console.WriteLine("// Subscribe with replay from 1 hour ago (matches databento-cpp overload):");
        Console.WriteLine("// var oneHourAgo = DateTimeOffset.UtcNow.AddHours(-1);");
        Console.WriteLine("// await client.SubscribeAsync(");
        Console.WriteLine("//     dataset: \"EQUS.MINI\",");
        Console.WriteLine("//     schema: Schema.Trades,");
        Console.WriteLine("//     symbols: new[] { \"NVDA\" },");
        Console.WriteLine("//     startTime: oneHourAgo");
        Console.WriteLine("// );");
        Console.WriteLine();
        Console.WriteLine("Example 3 (commented out): No replay - real-time only");
        Console.WriteLine("// Subscribe without replay (matches databento-cpp basic overload):");
        Console.WriteLine("// await client.SubscribeAsync(");
        Console.WriteLine("//     dataset: \"EQUS.MINI\",");
        Console.WriteLine("//     schema: Schema.Trades,");
        Console.WriteLine("//     symbols: new[] { \"NVDA\" }");
        Console.WriteLine("//     // No startTime parameter = no replay");
        Console.WriteLine("// );");
    }
}
