// Minimal reproduction of databento-cpp crash with invalid symbols
// Language: C# (.NET 8+)
// Issue: GetRangeAsync crashes with ExecutionEngineException when given invalid symbol

using Databento.Client.Builders;
using Databento.Client.Models;

// NOTE: Replace with your actual API key
var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
    ?? throw new InvalidOperationException("DATABENTO_API_KEY environment variable not set");

Console.WriteLine("Testing databento-cpp crash with invalid symbol...");
Console.WriteLine();

try
{
    // Create historical client
    await using var client = new HistoricalClientBuilder()
        .WithApiKey(apiKey)
        .Build();

    Console.WriteLine("Requesting data for INVALID symbol 'CL' (should be 'CL.FUT')...");

    var startTime = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    var endTime = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero);

    // This should throw DbentoException for invalid symbol
    // But instead causes ExecutionEngineException (process crash)
    await foreach (var record in client.GetRangeAsync(
        dataset: "GLBX.MDP3",
        schema: Schema.Trades,
        symbols: new[] { "CL" },  // ❌ Invalid symbol
        startTime: startTime,
        endTime: endTime))
    {
        Console.WriteLine($"Received: {record}");
    }

    Console.WriteLine("✅ No crash (unexpected)");
}
catch (Exception ex)
{
    Console.WriteLine($"Exception type: {ex.GetType().Name}");
    Console.WriteLine($"Message: {ex.Message}");

    if (ex is System.ExecutionEngineException)
    {
        Console.WriteLine("💥 CRASH: ExecutionEngineException detected!");
    }
}
