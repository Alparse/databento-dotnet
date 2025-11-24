using Databento.Client.Builders;
using Databento.Client.Models;

Console.WriteLine("=== Comparing Batch vs GetRange with Future Dates ===");
Console.WriteLine();

var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
    ?? throw new InvalidOperationException("DATABENTO_API_KEY not set");

Console.WriteLine("✓ API key found");
Console.WriteLine();

var startTime = new DateTimeOffset(DateTime.Parse("5/1/2025"), TimeSpan.Zero);
var endTime = new DateTimeOffset(DateTime.Parse("11/18/2025"), TimeSpan.Zero);
string dataset = "GLBX.MDP3";
Schema schema = Schema.Ohlcv1D;
string[] symbols = ["CLZ5"];

Console.WriteLine($"Testing with CLZ5, {startTime:yyyy-MM-dd} to {endTime:yyyy-MM-dd}");
Console.WriteLine("(Future dates - likely no data)");
Console.WriteLine();

// Test 1: Batch API (known to work)
Console.WriteLine("Test 1: BatchSubmitJob (should handle gracefully)");
try
{
    await using var client = new HistoricalClientBuilder().WithApiKey(apiKey).Build();

    var jobInfo = await client.BatchSubmitJobAsync(
        dataset, symbols, schema, startTime, endTime);

    Console.WriteLine($"  ✓ Batch job submitted: {jobInfo.Id}");
}
catch (Exception ex)
{
    Console.WriteLine($"  ✓ Caught exception: {ex.GetType().Name}");
    Console.WriteLine($"     {ex.Message}");
}

Console.WriteLine();

// Test 2: GetRange API (crashes)
Console.WriteLine("Test 2: GetRangeAsync (currently crashes)");
try
{
    await using var client = new HistoricalClientBuilder().WithApiKey(apiKey).Build();

    int count = 0;
    await foreach (var record in client.GetRangeAsync(dataset, schema, symbols, startTime, endTime))
    {
        count++;
    }

    Console.WriteLine($"  ✓ SUCCESS: Received {count} records");
}
catch (Exception ex)
{
    Console.WriteLine($"  ✓ Caught exception: {ex.GetType().Name}");
    Console.WriteLine($"     {ex.Message}");
}

Console.WriteLine();
Console.WriteLine("=== Tests complete ===");
