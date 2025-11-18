using Databento.Client.Builders;
using Databento.Client.Models;
using Databento.Interop;

namespace LiveAuthentication.Example;

/// <summary>
/// Demonstrates Live client authentication pattern matching the C++ example:
/// LiveBlocking::Builder().SetKeyFromEnv().SetDataset("EQUS.MINI").BuildBlocking()
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Databento Live Client Authentication Example");
        Console.WriteLine("=============================================\n");

        // Read API key from environment variable (matching SetKeyFromEnv())
        var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("ERROR: DATABENTO_API_KEY environment variable is not set.");
            Console.WriteLine();
            Console.WriteLine("Please set your API key:");
            Console.WriteLine("  Windows (PowerShell): $env:DATABENTO_API_KEY=\"your-api-key\"");
            Console.WriteLine("  Linux/macOS:          export DATABENTO_API_KEY=\"your-api-key\"");
            Console.WriteLine();
            Console.WriteLine("You can get your API key from: https://databento.com/portal/keys");
            return;
        }

        Console.WriteLine("✓ API key found in environment variable");
        Console.WriteLine($"  Key: {apiKey.Substring(0, 8)}... (masked)");
        Console.WriteLine();

        try
        {
            // Establish connection and authenticate
            // This matches the C++ pattern:
            // auto client = databento::LiveBlocking::Builder()
            //                   .SetKeyFromEnv()
            //                   .SetDataset("EQUS.MINI")
            //                   .BuildBlocking();
            await using var client = new LiveClientBuilder()
                .WithApiKey(apiKey)
                .WithDataset("EQUS.MINI")  // Default dataset
                .Build();

            Console.WriteLine("✓ Successfully created LiveClient");
            Console.WriteLine("  Default dataset: EQUS.MINI");
            Console.WriteLine();

            // Verify authentication by making a test subscription
            Console.WriteLine("Verifying authentication...");
            Console.WriteLine("Subscribing to NVDA trades on EQUS.MINI dataset");

            // Subscribe to data events
            var recordCount = 0;
            client.DataReceived += (sender, e) =>
            {
                recordCount++;
                if (recordCount <= 5)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Record #{recordCount}: {e.Record.GetType().Name}");
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

            // Start streaming
            Console.WriteLine("Starting live stream...");
            Console.WriteLine("(Receiving first 5 records to verify authentication)");
            Console.WriteLine();

            _ = await client.StartAsync();

            // Wait for a few records to confirm authentication works
            var timeout = DateTime.Now.AddSeconds(30);
            while (recordCount < 5 && DateTime.Now < timeout)
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
                // Expected during shutdown - ignore
            }

            Console.WriteLine();
            if (recordCount > 0)
            {
                Console.WriteLine($"✓ Authentication successful! Received {recordCount} records.");
            }
            else
            {
                Console.WriteLine("⚠ No records received within timeout period.");
                Console.WriteLine("  This could mean:");
                Console.WriteLine("  - Market is closed");
                Console.WriteLine("  - NVDA has no trades at this time");
                Console.WriteLine("  - API key may not have live data access");
            }
        }
        catch (DbentoAuthenticationException ex)
        {
            Console.WriteLine($"✗ Authentication failed: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Please check:");
            Console.WriteLine("  1. Your API key is correct");
            Console.WriteLine("  2. Your API key has live streaming permissions");
            Console.WriteLine("  3. You can view/manage keys at: https://databento.com/portal/keys");
        }
        catch (System.Threading.Channels.ChannelClosedException)
        {
            // Expected during cleanup - ignore
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Unexpected error: {ex.GetType().Name}");
            Console.WriteLine($"  Message: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("Authentication example complete.");
    }
}
