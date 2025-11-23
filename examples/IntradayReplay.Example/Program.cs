using Databento.Client.Builders;
using Databento.Client.Models;
using Databento.Interop;

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
            var lastProgressUpdate = DateTime.Now;

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
                        Console.WriteLine();
                        Console.WriteLine("=".PadRight(80, '='));
                        Console.WriteLine("✓ INTRADAY REPLAY COMPLETED");
                        Console.WriteLine("=".PadRight(80, '='));
                        Console.WriteLine($"  Total records received: {recordCount:N0}");
                        Console.WriteLine($"  Total trades received:  {tradeCount:N0}");
                        Console.WriteLine("  Now receiving real-time data...");
                        Console.WriteLine("=".PadRight(80, '='));
                        Console.WriteLine();
                    }
                }
                else if (e.Record is TradeMessage trade)
                {
                    tradeCount++;

                    // Decode and display first 10 trades
                    if (tradeCount <= 10)
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
                    else if (tradeCount == 11)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] (suppressing trade details, continuing to receive data...)");
                        Console.WriteLine();
                    }
                }
                else if (e.Record is SymbolMappingMessage symMap)
                {
                    // Show symbol mappings
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] SymbolMapping: {symMap}");
                }

                // Show progress every 5 seconds
                if ((DateTime.Now - lastProgressUpdate).TotalSeconds >= 5)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Progress: {recordCount:N0} total records, {tradeCount:N0} trades");
                    lastProgressUpdate = DateTime.Now;
                }
            };

            // Use full replay history (DateTimeOffset.MinValue)
            // This replays all available intraday data (typically last 24 hours)
            // The gateway will automatically limit to available replay window
            var replayStartTime = DateTimeOffset.MinValue;

            Console.WriteLine("Intraday Replay Configuration:");
            Console.WriteLine("  Replay Mode:  Full intraday history (last 24 hours)");
            Console.WriteLine("  Start Time:   DateTimeOffset.MinValue (requests all available)");
            Console.WriteLine("  Note:         Gateway automatically limits to available replay window");
            Console.WriteLine();

            await client.SubscribeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Trades,
                symbols: new[] { "NVDA", "AAPL" },  // Subscribe to 2 symbols for more data
                startTime: replayStartTime  // Request all available replay data
            );

            Console.WriteLine("✓ Subscribed to NVDA, AAPL trades with full intraday replay");
            Console.WriteLine("  Symbols: NVDA, AAPL");
            Console.WriteLine("  Schema:  Trades");
            Console.WriteLine("  Replay:  All available intraday data (up to last 24 hours)");
            Console.WriteLine();

            // Start streaming
            Console.WriteLine("Starting live stream with replay...");
            Console.WriteLine("(Receiving records until replay completes...)");
            Console.WriteLine();

            await client.StartAsync();

            // Wait for replay to complete (with generous timeout)
            var startWallTime = DateTime.Now;
            var maxWaitTime = TimeSpan.FromMinutes(10);  // 10-minute max wait

            Console.WriteLine("Waiting for replay to complete...");
            Console.WriteLine();

            while (DateTime.Now - startWallTime < maxWaitTime)
            {
                await Task.Delay(100);

                // Stop when we receive replay completed message
                if (replayCompleteReceived)
                {
                    // Wait a bit longer to receive a few real-time records
                    Console.WriteLine("Collecting 5 seconds of real-time data after replay...");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                    break;
                }
            }

            if (!replayCompleteReceived)
            {
                Console.WriteLine();
                Console.WriteLine("⚠ Warning: Timeout reached without receiving replay completion signal.");
                Console.WriteLine("  This may indicate replay is still ongoing or no replay completion message was sent.");
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
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("INTRADAY REPLAY EXAMPLE COMPLETED");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();
            Console.WriteLine("Summary:");
            Console.WriteLine($"  Replay mode:             Full intraday history (last 24 hours)");
            Console.WriteLine($"  Wall clock duration:     {(DateTime.Now - startWallTime).TotalSeconds:F1} seconds");
            Console.WriteLine($"  Total records received:  {recordCount:N0}");
            Console.WriteLine($"  Trade messages:          {tradeCount:N0}");
            Console.WriteLine($"  Other messages:          {(recordCount - tradeCount):N0}");
            Console.WriteLine($"  Replay completed:        {(replayCompleteReceived ? "✓ Yes" : "✗ No (timeout/ongoing)")}");
            Console.WriteLine();
            Console.WriteLine("What Happened:");
            Console.WriteLine("  1. Subscribed to NVDA, AAPL trades with full intraday replay");
            Console.WriteLine("  2. Gateway replayed all available data from last 24 hours");
            Console.WriteLine($"  3. Received {tradeCount:N0} trades from replay history");
            Console.WriteLine("  4. " + (replayCompleteReceived
                ? "Replay completed, transitioned to real-time data"
                : "Replay may still be ongoing or no completion signal sent"));
            Console.WriteLine();
            Console.WriteLine("TradeMessage Fields:");
            Console.WriteLine("  - Timestamp (ts_event):  Nanosecond precision timestamp");
            Console.WriteLine("  - Instrument ID:         Numeric identifier for symbol");
            Console.WriteLine("  - Price:                 Decimal price (converted from fixed-point)");
            Console.WriteLine("  - Size:                  Number of shares/contracts");
            Console.WriteLine("  - Side:                  Ask='A', Bid='B', None='N'");
            Console.WriteLine("  - Action:                Trade, Fill, etc.");
            Console.WriteLine("  - Flags:                 Last, Snapshot, etc.");
            Console.WriteLine();
            Console.WriteLine("Key Features:");
            Console.WriteLine("  ✓ Full intraday replay (all available data)");
            Console.WriteLine("  ✓ No artificial cutoffs - runs until replay completes");
            Console.WriteLine("  ✓ Progress tracking every 5 seconds");
            Console.WriteLine("  ✓ Replay completion detection");
            Console.WriteLine("  ✓ Automatic transition to real-time after replay");
            Console.WriteLine("=".PadRight(80, '='));
        }
        catch (DbentoAuthenticationException ex)
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
            if (ex.StackTrace != null)
            {
                Console.WriteLine($"  Stack trace:");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
