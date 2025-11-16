using Databento.Client.Builders;
using Databento.Client.Models;

namespace SnapshotSubscription.Example;

/// <summary>
/// Demonstrates SubscribeWithSnapshot matching the C++ example:
///
/// auto client = LiveBlocking::Builder()
///                   .SetKey("YOUR_API_KEY")
///                   .SetDataset("GLBX.MDP3")
///                   .BuildBlocking();
/// client.SubscribeWithSnapshot({"ES.c.0"}, Schema::Mbo, SType::Continuous);
///
/// SubscribeWithSnapshot is ONLY supported for the MBO schema.
/// It provides a snapshot of the recent order book state without replaying the whole session.
///
/// Key points:
/// - Only supported for MBO (Market By Order) schema
/// - Takes up to 2,000 symbols per request
/// - No unsubscribe method - subscriptions end when client disconnects or Stop() is called
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Databento SubscribeWithSnapshot Example");
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
            // Build the client
            // This matches: LiveBlocking::Builder().SetKeyFromEnv().SetDataset("EQUS.MINI").BuildBlocking()
            await using var client = new LiveClientBuilder()
                .WithApiKey(apiKey)
                .WithDataset("EQUS.MINI")
                .Build();

            Console.WriteLine("✓ Created LiveClient with dataset: EQUS.MINI");
            Console.WriteLine();

            // Track messages
            var recordCount = 0;
            var mboCount = 0;
            var snapshotMboCount = 0;

            // Subscribe to data events
            client.DataReceived += (sender, e) =>
            {
                recordCount++;

                if (e.Record is MboMessage mbo)
                {
                    mboCount++;

                    // Check if this is a snapshot message (has Snapshot flag)
                    bool isSnapshot = (mbo.Flags & 128) != 0;
                    if (isSnapshot && mbo.Action == Databento.Client.Models.Action.Add)
                    {
                        snapshotMboCount++;
                    }

                    // Display first 10 MBO messages
                    if (mboCount <= 10)
                    {
                        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(mbo.TimestampNs / 1_000_000);
                        var snapshotTag = isSnapshot ? " [SNAPSHOT]" : "";

                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] MBO #{mboCount}{snapshotTag}:");
                        Console.WriteLine($"  Order ID: {mbo.OrderId}");
                        Console.WriteLine($"  Price:    ${mbo.PriceDecimal:F2}");
                        Console.WriteLine($"  Size:     {mbo.Size}");
                        Console.WriteLine($"  Side:     {mbo.Side}");
                        Console.WriteLine($"  Action:   {mbo.Action}");
                        Console.WriteLine($"  Time:     {timestamp:HH:mm:ss.fff} UTC");
                        Console.WriteLine();
                    }
                }
                else if (e.Record is SystemMessage sysMsg)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {sysMsg}");
                    Console.WriteLine();
                }
            };

            // Subscribe with snapshot
            // This matches: client.SubscribeWithSnapshot({"ES.c.0"}, Schema::Mbo, SType::Continuous);
            // Adapted to use NVDA on EQUS.MINI
            Console.WriteLine("Calling SubscribeWithSnapshot:");
            Console.WriteLine("  Symbol: NVDA");
            Console.WriteLine("  Schema: Mbo (Market By Order) - REQUIRED");
            Console.WriteLine("  Mode:   With snapshot");
            Console.WriteLine();

            await client.SubscribeWithSnapshotAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Mbo,
                symbols: new[] { "NVDA" }
            );

            Console.WriteLine("✓ SubscribeWithSnapshot successful");
            Console.WriteLine();

            // Start streaming
            Console.WriteLine("Starting live stream...");
            Console.WriteLine("(Receiving order book snapshot + live updates for 10 seconds)");
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
            Console.WriteLine("SubscribeWithSnapshot example completed!");
            Console.WriteLine();
            Console.WriteLine("Summary:");
            Console.WriteLine($"  Total records:        {recordCount}");
            Console.WriteLine($"  MBO messages:         {mboCount}");
            Console.WriteLine($"  Snapshot MBO records: {snapshotMboCount}");
            Console.WriteLine();

            Console.WriteLine("API Mapping:");
            Console.WriteLine("  C++: client.SubscribeWithSnapshot({\"ES.c.0\"}, Schema::Mbo, SType::Continuous);");
            Console.WriteLine("  C#:  await client.SubscribeWithSnapshotAsync(dataset, Schema.Mbo, symbols);");
            Console.WriteLine();

            Console.WriteLine("Important Notes:");
            Console.WriteLine("  • ONLY supported for MBO (Market By Order) schema");
            Console.WriteLine("  • Provides recent order book snapshot without full replay");
            Console.WriteLine("  • Snapshot records have the Snapshot flag (128) set");
            Console.WriteLine("  • After snapshot, receives real-time order updates");
            Console.WriteLine("  • Supports up to 2,000 symbols per request");
            Console.WriteLine("  • No unsubscribe method - ends when Stop() is called");
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
            Console.WriteLine();

            if (ex.Message.Contains("MBO") || ex.Message.Contains("schema"))
            {
                Console.WriteLine("Note: SubscribeWithSnapshot requires MBO schema support.");
                Console.WriteLine("      MBO may not be available for all datasets.");
                Console.WriteLine("      Typically available for futures datasets like GLBX.MDP3.");
            }
        }
    }
}
