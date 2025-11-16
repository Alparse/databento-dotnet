using Databento.Client.Builders;
using Databento.Client.Models;

namespace Snapshot.Example;

/// <summary>
/// Demonstrates snapshot functionality matching the C++ example:
/// client.SubscribeWithSnapshot({"ES.c.0"}, Schema::Mbo, SType::Continuous);
///
/// Snapshot allows you to request a snapshot of the recent order book state
/// without replaying the whole trading session. This is only supported for the MBO schema.
///
/// Note: This requires a dataset with MBO schema support (e.g., GLBX.MDP3 for futures).
/// More details: https://databento.com/docs/api-reference-live/live-api-snapshot
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Databento Snapshot Example");
        Console.WriteLine("==========================\n");

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
            // Create live client with EQUS.MINI dataset
            await using var client = new LiveClientBuilder()
                .WithApiKey(apiKey)
                .WithDataset("EQUS.MINI")  // Using EQUS.MINI as requested
                .Build();

            Console.WriteLine("✓ Created LiveClient with dataset: EQUS.MINI");
            Console.WriteLine();

            // Track record counts
            var recordCount = 0;
            var mboCount = 0;
            var snapshotCount = 0;

            // Subscribe to data events
            client.DataReceived += (sender, e) =>
            {
                recordCount++;

                // Check for system messages
                if (e.Record is SystemMessage sysMsg)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] SystemMessage: {sysMsg}");
                }
                else if (e.Record is SymbolMappingMessage symMap)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] SymbolMapping: {symMap}");
                }
                else if (e.Record is MboMessage mbo)
                {
                    mboCount++;

                    // Count snapshot messages (Action.Add with Snapshot flag)
                    if ((mbo.Flags & 128) != 0 && mbo.Action == Databento.Client.Models.Action.Add)
                    {
                        snapshotCount++;
                    }

                    // Display first 20 MBO messages in detail to show snapshot data
                    if (mboCount <= 20)
                    {
                        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(mbo.TimestampNs / 1_000_000);
                        var isSnapshot = (mbo.Flags & 128) != 0 ? " [SNAPSHOT]" : "";

                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] MBO #{mboCount}{isSnapshot}:");
                        Console.WriteLine($"  Timestamp:     {timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC");
                        Console.WriteLine($"  Order ID:      {mbo.OrderId}");
                        Console.WriteLine($"  Price:         ${mbo.PriceDecimal:F2}");
                        Console.WriteLine($"  Size:          {mbo.Size}");
                        Console.WriteLine($"  Side:          {mbo.Side}");
                        Console.WriteLine($"  Action:        {mbo.Action}");
                        Console.WriteLine($"  Flags:         {mbo.Flags}");
                        Console.WriteLine();
                    }
                    else if (mboCount % 1000 == 0)
                    {
                        // Show progress every 1000 MBO messages
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Progress: {mboCount} MBO messages ({snapshotCount} snapshot records)");
                    }
                }
            };

            // Subscribe with snapshot to NVDA
            // This matches the C++ example: client.SubscribeWithSnapshot({"ES.c.0"}, Schema::Mbo, SType::Continuous);
            // Adapted to use NVDA on EQUS.MINI dataset
            // Note: MBO schema may not be available for all datasets. Requires appropriate license.
            Console.WriteLine("Subscribing with SNAPSHOT to NVDA");
            Console.WriteLine("This requests the recent order book state without replaying the whole session");
            Console.WriteLine();

            await client.SubscribeWithSnapshotAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Mbo,
                symbols: new[] { "NVDA" }
            );

            Console.WriteLine("✓ Subscribed to NVDA MBO data with snapshot");
            Console.WriteLine("  Symbol: NVDA");
            Console.WriteLine("  Schema: MBO (Market By Order)");
            Console.WriteLine("  Mode:   With snapshot (recent order book state)");
            Console.WriteLine();

            // Start streaming
            Console.WriteLine("Starting live stream with snapshot...");
            Console.WriteLine("(Receiving snapshot + live data, will stop after 30 seconds)");
            Console.WriteLine();

            await client.StartAsync();

            // Wait for data and timeout after 30 seconds
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromSeconds(30);

            while (DateTime.Now - startTime < timeout)
            {
                await Task.Delay(100);
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
            Console.WriteLine("Snapshot example completed successfully!");
            Console.WriteLine();
            Console.WriteLine("Summary:");
            Console.WriteLine($"  Total records received:  {recordCount}");
            Console.WriteLine($"  MBO messages:            {mboCount}");
            Console.WriteLine($"  Snapshot records:        {snapshotCount}");
            Console.WriteLine($"  Other messages:          {recordCount - mboCount}");
            Console.WriteLine();
            Console.WriteLine("MBO Message Fields Decoded:");
            Console.WriteLine("  - Timestamp (ts_event):  Nanosecond precision timestamp");
            Console.WriteLine("  - Order ID:              Unique order identifier");
            Console.WriteLine("  - Price:                 Order price converted from fixed-point to decimal");
            Console.WriteLine("  - Size:                  Order quantity");
            Console.WriteLine("  - Side:                  Ask or Bid side");
            Console.WriteLine("  - Action:                Add, Modify, Cancel, Clear, Trade, Fill");
            Console.WriteLine("  - Flags:                 Bit flags (128 = Last/Snapshot)");
            Console.WriteLine();
            Console.WriteLine("Notes:");
            Console.WriteLine("  - Snapshot provides recent order book state without full replay");
            Console.WriteLine("  - Only supported for MBO (Market By Order) schema");
            Console.WriteLine("  - Snapshot records have the Snapshot flag (128) set");
            Console.WriteLine("  - After snapshot, you receive real-time order updates");
            Console.WriteLine("  - MBO shows individual orders (vs aggregated levels in MBP)");
            Console.WriteLine("  - MBO schema availability depends on dataset (e.g., GLBX.MDP3 for futures)");
            Console.WriteLine("  - If gateway closes immediately, the dataset may not support MBO or market is closed");
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
