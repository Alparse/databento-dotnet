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

// Query historical trades
await foreach (var record in client.GetRangeAsync(
    dataset: "EQUS.MINI",
    schema: Schema.Trades,
    symbols: new[] { "NVDA" },
    startTime: startTime,
    endTime: endTime))
{
    Console.WriteLine($"Historical record: {record}");
}