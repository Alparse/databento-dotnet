using Databento.Client.Builders;
using Databento.Client.Models;

// Get API key from environment variable (secure)
var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
    ?? throw new InvalidOperationException("DATABENTO_API_KEY environment variable not set");

// Create historical client
await using var client = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();

// Define time range - Static trading day: November 11-12, 2025
var startTime = new DateTimeOffset(2025, 11, 11, 0, 0, 0, TimeSpan.Zero); // 11/11/2025 00:00 UTC
var endTime = new DateTimeOffset(2025, 11, 12, 23, 59, 59, TimeSpan.Zero);   // 11/12/2025 23:59 UTC

Console.WriteLine($"Querying historical trades from {startTime:yyyy-MM-dd} to {endTime:yyyy-MM-dd}");
Console.WriteLine("(Limited to 60 seconds or 1000 records for demo purposes)\n");

// Query historical trades with timeout
var recordCount = 0;
var maxRecords = 1000;
var timeout = TimeSpan.FromSeconds(60);
var cts = new CancellationTokenSource(timeout);

try
{
    await foreach (var record in client.GetRangeAsync(
        dataset: "EQUS.MINI",
        schema: Schema.Trades,
        symbols: new[] { "NVDA" },
        startTime: startTime,
        endTime: endTime).WithCancellation(cts.Token))
    {
        recordCount++;

        // Show first 10 records, then summary every 100
        if (recordCount <= 10 || recordCount % 100 == 0)
        {
            Console.WriteLine($"[{recordCount}] {record}");
        }

        // Limit for demo purposes
        if (recordCount >= maxRecords)
        {
            Console.WriteLine($"\nReached demo limit of {maxRecords} records");
            break;
        }
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine($"\nTimeout reached after {timeout.TotalSeconds} seconds");
}

Console.WriteLine($"\n✓ Processed {recordCount} historical records");
Console.WriteLine("\n=== Historical.Readme.Example Complete ===");