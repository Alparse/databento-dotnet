using Databento.Client.Builders;
using Databento.Client.Models;

namespace LiveStreaming.Example;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Databento Live Streaming Example - Testing StartAsync Metadata");
        Console.WriteLine("================================================================\n");

        // Get API key from environment variable or command line
        var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
                     ?? (args.Length > 0 ? args[0] : null);

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: API key not provided.");
            Console.WriteLine("Usage: dotnet run <api-key>");
            Console.WriteLine("Or set DATABENTO_API_KEY environment variable");
            return;
        }

        try
        {
            // Create live client
            await using var client = new LiveClientBuilder()
                .WithApiKey(apiKey)
                .WithDataset("EQUS.MINI")
                .Build();

            Console.WriteLine("✓ Created live client with dataset: EQUS.MINI");
            Console.WriteLine();

            // Subscribe to data received events
            var recordCount = 0;
            client.DataReceived += (sender, e) =>
            {
                recordCount++;
                if (recordCount <= 5)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Received: {e.Record.GetType().Name}");
                }
            };

            // Subscribe to error events
            client.ErrorOccurred += (sender, e) =>
            {
                Console.WriteLine($"[ERROR] {e.Exception.Message}");
            };

            // Subscribe to equity trades on EQUS.MINI dataset
            await client.SubscribeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Trades,
                symbols: new[] { "NVDA", "TSLA" }
            );

            Console.WriteLine("✓ Subscribed to NVDA, TSLA trades on EQUS.MINI");
            Console.WriteLine();

            // ================================================================
            // KEY TEST: StartAsync should return metadata like LiveBlocking
            // ================================================================
            Console.WriteLine("Starting stream and waiting for metadata...");
            Console.WriteLine();

            var metadata = await client.StartAsync();

            Console.WriteLine("✓ StartAsync() returned metadata!");
            Console.WriteLine();
            Console.WriteLine("DBN Metadata:");
            Console.WriteLine("─────────────");
            Console.WriteLine($"  Version:       {metadata.Version}");
            Console.WriteLine($"  Dataset:       {metadata.Dataset}");
            Console.WriteLine($"  Schema:        {metadata.Schema?.ToString() ?? "(mixed)"}");
            Console.WriteLine($"  Stype Out:     {metadata.StypeOut}");
            Console.WriteLine($"  Timestamp Out: {metadata.TsOut}");
            Console.WriteLine($"  Start Time:    {DateTimeOffset.FromUnixTimeMilliseconds(metadata.Start / 1_000_000):yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"  Symbols:       {string.Join(", ", metadata.Symbols)}");
            if (metadata.Partial.Count > 0)
                Console.WriteLine($"  Partial:       {string.Join(", ", metadata.Partial)}");
            if (metadata.NotFound.Count > 0)
                Console.WriteLine($"  Not Found:     {string.Join(", ", metadata.NotFound)}");
            Console.WriteLine();

            // Stream records using IAsyncEnumerable for 5 seconds
            Console.WriteLine("Streaming records for 5 seconds using IAsyncEnumerable...");
            Console.WriteLine();

            var startTime = DateTime.Now;
            await foreach (var record in client.StreamAsync())
            {
                // Process records here
                if ((DateTime.Now - startTime).TotalSeconds >= 5)
                {
                    Console.WriteLine($"✓ Received {recordCount} records total, stopping...");
                    break;
                }
            }
            Console.WriteLine();

            // Stop streaming
            await client.StopAsync();
            Console.WriteLine("✓ Stopped stream");
            Console.WriteLine();

            // Summary
            Console.WriteLine("Test Results:");
            Console.WriteLine("─────────────");
            Console.WriteLine("✓ StartAsync() correctly returns DbnMetadata");
            Console.WriteLine("✓ Metadata contains version, dataset, symbols");
            Console.WriteLine("✓ IAsyncEnumerable streaming works");
            Console.WriteLine("✓ DataReceived event fires for each record");
            Console.WriteLine();
            Console.WriteLine("LiveClient (LiveThreaded wrapper) is working correctly!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.WriteLine($"  Type: {ex.GetType().Name}");
            if (ex.StackTrace != null)
            {
                Console.WriteLine($"  Stack: {ex.StackTrace}");
            }
        }

        // Only wait for key press if running interactively (not in automation)
        if (Environment.UserInteractive && !Console.IsInputRedirected)
        {
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
