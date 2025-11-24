#!/usr/bin/env dotnet-script
#r "nuget: Databento, 3.0.23-beta"

using Databento.Client;
using Databento.Common;

var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("ERROR: DATABENTO_API_KEY not set");
    return 1;
}

Console.WriteLine("=== Testing Future Dates with Fixed Code ===");
Console.WriteLine();

try
{
    using var client = new HistoricalClientBuilder()
        .WithApiKey(apiKey)
        .Build();

    Console.WriteLine("✓ Created Historical Client");
    Console.WriteLine();

    // CRITICAL TEST: Future dates with CLZ5 (the failing case)
    var dataset = "GLBX.MDP3";
    var schema = Schema.Ohlcv1D;
    var symbols = new[] { "CLZ5" };
    var startTime = new DateTimeOffset(2025, 5, 1, 0, 0, 0, TimeSpan.Zero);
    var endTime = new DateTimeOffset(2025, 11, 18, 0, 0, 0, TimeSpan.Zero);

    Console.WriteLine($"Test Parameters:");
    Console.WriteLine($"  Dataset: {dataset}");
    Console.WriteLine($"  Schema: {schema}");
    Console.WriteLine($"  Symbol: {symbols[0]}");
    Console.WriteLine($"  Start: {startTime:yyyy-MM-dd}");
    Console.WriteLine($"  End: {endTime:yyyy-MM-dd}");
    Console.WriteLine();

    Console.WriteLine("Calling GetRangeAsync (this used to crash)...");
    Console.WriteLine();

    int count = 0;
    try
    {
        await foreach (var record in client.GetRangeAsync(dataset, schema, symbols, startTime, endTime))
        {
            count++;
            if (count == 1)
            {
                Console.WriteLine($"  ✓ First record received");
            }
        }

        Console.WriteLine($"✅ SUCCESS: Received {count} records without crashing!");
    }
    catch (DbentoException ex)
    {
        Console.WriteLine($"✅ SUCCESS: Got proper exception instead of crash");
        Console.WriteLine($"   Exception: {ex.Message}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ ERROR: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
    return 1;
}

Console.WriteLine();
Console.WriteLine("=== Test Complete ===");
return 0;
