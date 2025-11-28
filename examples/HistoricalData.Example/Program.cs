using Databento.Client.Builders;
using Databento.Client.Models;

namespace HistoricalData.Example;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Databento Historical Data Example");
        Console.WriteLine("==================================\n");

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
            // Create historical client
            await using var client = new HistoricalClientBuilder()
                .WithApiKey(apiKey)
                .Build();

            Console.WriteLine("✓ Created historical client");

            // Define time range (January 2, 2024 - one trading day)
            var startTime = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero);
            var endTime = new DateTimeOffset(2024, 1, 2, 23, 59, 59, TimeSpan.Zero);

            Console.WriteLine($"✓ Querying data from {startTime:yyyy-MM-dd HH:mm:ss} to {endTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"✓ Symbol: NVDA (NVIDIA Corporation)");
            Console.WriteLine($"✓ Schema: Trades\n");

            // Query historical trades
            var count = 0;
            await foreach (var record in client.GetRangeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Trades,
                symbols: new[] { "NVDA" },
                startTime: startTime,
                endTime: endTime))
            {
                count++;

                // Print first few records
                if (count <= 10)
                {
                    Console.WriteLine($"[{count}] {record}");
                }
                else if (count == 11)
                {
                    Console.WriteLine("...");
                }

                // Limit for demo purposes
                if (count >= 1000)
                {
                    break;
                }
            }

            Console.WriteLine($"\n✓ Processed {count} historical records");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        // Only wait for key press if running interactively (not in automation)
        if (Environment.UserInteractive && !Console.IsInputRedirected)
        {
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
