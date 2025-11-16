using Databento.Client.Builders;

Console.WriteLine("=== Tester2.Example ===");
Console.WriteLine();

// Get API key from environment variable
var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
    ?? throw new InvalidOperationException(
        "DATABENTO_API_KEY environment variable is not set. " +
        "Set it with your API key to authenticate.");

Console.WriteLine("Connecting to Databento API...");

// Create a historical client
var client = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();

Console.WriteLine("Successfully authenticated!");
Console.WriteLine();

// Example: List available datasets
Console.WriteLine("Fetching available datasets...");
var datasets = await client.ListDatasetsAsync();

Console.WriteLine($"Available datasets ({datasets.Count}):");
foreach (var dataset in datasets)
{
    Console.WriteLine($"  - {dataset}");
}
Console.WriteLine();

Console.WriteLine("Tester2.Example completed successfully!");
