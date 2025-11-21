using Databento.Client.Builders;
using Databento.Client.Models;

Console.WriteLine("=== Databento Historical Future Dates Test ===");
Console.WriteLine();

var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("ERROR: DATABENTO_API_KEY environment variable not set!");
    return 1;
}

// TEST: Future dates that trigger X-Warning header
// This previously caused AccessViolationException
Console.WriteLine("Testing Historical API with future dates (May-Nov 2025)...");
Console.WriteLine("Expected: API warning about 'reduced quality' dates");
Console.WriteLine("Previous behavior: AccessViolationException crash");
Console.WriteLine("Fixed behavior: Successfully receive data with warning logged");
Console.WriteLine();

try
{
    await using var client = new HistoricalClientBuilder()
        .WithApiKey(apiKey)
        .Build();

    var startTime = new DateTimeOffset(DateTime.Parse("5/1/2025"), TimeSpan.Zero);
    var endTime = new DateTimeOffset(DateTime.Parse("11/18/2025"), TimeSpan.Zero);

    Console.WriteLine($"Dataset: GLBX.MDP3");
    Console.WriteLine($"Symbol: CLZ5");
    Console.WriteLine($"Schema: Ohlcv1D");
    Console.WriteLine($"Range: {startTime:yyyy-MM-dd} to {endTime:yyyy-MM-dd}");
    Console.WriteLine();
    Console.WriteLine("Fetching data...");
    Console.WriteLine();

    int count = 0;
    await foreach (var record in client.GetRangeAsync(
        dataset: "GLBX.MDP3",
        schema: Schema.Ohlcv1D,
        symbols: ["CLZ5"],
        startTime: startTime,
        endTime: endTime))
    {
        count++;
        if (count <= 5)
        {
            Console.WriteLine($"Record {count}: {record}");
        }
        else if (count == 6)
        {
            Console.WriteLine("... (suppressing further output)");
        }
    }

    Console.WriteLine();
    Console.WriteLine($"✓ SUCCESS: Received {count} records without crashing!");
    Console.WriteLine();
    Console.WriteLine("The bug is fixed if you see this message.");
    return 0;
}
catch (AccessViolationException ex)
{
    Console.WriteLine();
    Console.WriteLine("✗ FAILED: AccessViolationException still occurs!");
    Console.WriteLine($"Exception: {ex.Message}");
    Console.WriteLine();
    Console.WriteLine("The bug is NOT fixed.");
    return 1;
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine($"✗ FAILED: Unexpected exception: {ex.GetType().Name}");
    Console.WriteLine($"Message: {ex.Message}");
    Console.WriteLine($"StackTrace: {ex.StackTrace}");
    return 1;
}
