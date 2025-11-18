using Databento.Client.Builders;
using Databento.Client.Models;
using Databento.Interop;

namespace LiveBlocking.Example;

/// <summary>
/// Demonstrates LiveClient initialization matching the C++ LiveBlocking examples:
///
/// // Pass API key as an argument (not recommended for production)
/// auto client1 = LiveBlocking::Builder()
///                    .SetKey("YOUR_API_KEY")
///                    .SetDataset(Dataset::EqusMini)
///                    .BuildBlocking();
///
/// // Or, pass as `DATABENTO_API_KEY` environment variable (recommended)
/// auto client2 = LiveBlocking::Builder()
///                    .SetKeyFromEnv()
///                    .SetDataset(Dataset::EqusMini)
///                    .BuildBlocking();
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Databento LiveBlocking Client Example");
        Console.WriteLine("=====================================\n");

        // ===================================================================
        // Method 1: Pass API key as argument (NOT recommended for production)
        // This matches: LiveBlocking::Builder().SetKey("...").SetDataset(...).BuildBlocking()
        // ===================================================================
        Console.WriteLine("Method 1: API Key as Argument (NOT recommended for production)");
        Console.WriteLine("---------------------------------------------------------------");
        Console.WriteLine("In production, use Method 2 (environment variable) instead.\n");

        // Uncomment to use this method (replace with your actual API key):
        // var client1 = new LiveClientBuilder()
        //     .WithApiKey("db-YourActualApiKeyHere")
        //     .WithDataset("EQUS.MINI")
        //     .Build();
        // Console.WriteLine("✓ Created LiveClient with API key argument");
        // Console.WriteLine("  Dataset: EQUS.MINI\n");
        // await client1.DisposeAsync();

        Console.WriteLine("(Skipped - not recommended for production use)\n");

        // ===================================================================
        // Method 2: Read API key from environment variable (RECOMMENDED)
        // This matches: LiveBlocking::Builder().SetKeyFromEnv().SetDataset(...).BuildBlocking()
        // ===================================================================
        Console.WriteLine("Method 2: API Key from Environment Variable (RECOMMENDED)");
        Console.WriteLine("----------------------------------------------------------");
        Console.WriteLine("Reads from DATABENTO_API_KEY environment variable\n");

        var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("✗ DATABENTO_API_KEY environment variable is not set.");
            Console.WriteLine("  Please set your API key and try again.");
            Console.WriteLine();
            Console.WriteLine("Example (Windows):");
            Console.WriteLine("  set DATABENTO_API_KEY=your-api-key-here");
            Console.WriteLine();
            Console.WriteLine("Example (Linux/Mac):");
            Console.WriteLine("  export DATABENTO_API_KEY=your-api-key-here");
            return;
        }

        try
        {
            // This matches the C++ pattern:
            // auto client = LiveBlocking::Builder()
            //                  .SetKeyFromEnv()
            //                  .SetDataset(Dataset::EqusMini)
            //                  .BuildBlocking();
            await using var client = new LiveClientBuilder()
                .WithApiKey(apiKey)  // Reads from DATABENTO_API_KEY
                .WithDataset("EQUS.MINI")  // Corresponds to Dataset::EqusMini
                .Build();

            Console.WriteLine("✓ Created LiveClient from environment variable");
            Console.WriteLine("  Dataset: EQUS.MINI");
            Console.WriteLine();

            // ===================================================================
            // Verify the client works with a simple subscription
            // ===================================================================
            Console.WriteLine("Verifying client connectivity...");
            Console.WriteLine("Subscribing to NVDA trades for 5 seconds\n");

            var recordCount = 0;
            var tradeCount = 0;

            // Subscribe to data events
            client.DataReceived += (sender, e) =>
            {
                recordCount++;
                if (e.Record is TradeMessage)
                {
                    tradeCount++;
                }
                else if (e.Record is SystemMessage sysMsg)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {sysMsg}");
                }
            };

            // Subscribe to NVDA trades
            await client.SubscribeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Trades,
                symbols: new[] { "NVDA" }
            );

            Console.WriteLine("✓ Subscribed to NVDA trades");
            Console.WriteLine();

            // Start streaming (discarding metadata for this example)
            _ = await client.StartAsync();
            Console.WriteLine("✓ Started streaming");
            Console.WriteLine("  (Receiving data for 5 seconds...)\n");

            // Wait 5 seconds
            await Task.Delay(5000);

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
            Console.WriteLine("✓ Client verification complete!");
            Console.WriteLine();
            Console.WriteLine($"  Total records received: {recordCount}");
            Console.WriteLine($"  Trade messages:         {tradeCount}");
            Console.WriteLine($"  Other messages:         {recordCount - tradeCount}");
            Console.WriteLine();

            Console.WriteLine("Summary:");
            Console.WriteLine("--------");
            Console.WriteLine("✓ LiveClient successfully created using environment variable");
            Console.WriteLine("✓ Dataset configured: EQUS.MINI");
            Console.WriteLine("✓ Connectivity verified with NVDA subscription");
            Console.WriteLine();
            Console.WriteLine("Builder Pattern Mapping:");
            Console.WriteLine("  C++:  LiveBlocking::Builder().SetKeyFromEnv().SetDataset(...).BuildBlocking()");
            Console.WriteLine("  C#:   new LiveClientBuilder().WithApiKey(apiKey).WithDataset(...).Build()");
        }
        catch (DbentoAuthenticationException ex)
        {
            Console.WriteLine($"✗ Authentication failed: {ex.Message}");
            Console.WriteLine("  Please verify your API key is correct.");
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
        Console.WriteLine("Additional Builder Options:");
        Console.WriteLine("---------------------------");
        Console.WriteLine("• .WithSendTimestampOut(bool)      - Include gateway send timestamps");
        Console.WriteLine("• .WithUpgradePolicy(policy)       - Set version upgrade policy");
        Console.WriteLine("• .WithHeartbeatInterval(timespan) - Configure heartbeat interval");
        Console.WriteLine("• .WithLogger(logger)              - Add diagnostic logging");
    }
}
