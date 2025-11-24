#!/usr/bin/env dotnet-script
#r "src/Databento.Client/bin/Release/net8.0/Databento.Client.dll"
#r "src/Databento.Interop/bin/Release/net8.0/Databento.Interop.dll"

using Databento.Client.Builders;
using Databento.Interop;
using Databento.Client.Models;

Console.WriteLine("Testing invalid symbol with minimal example...");

var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("ERROR: DATABENTO_API_KEY not set");
    return;
}

try
{
    await using var client = new HistoricalClientBuilder()
        .WithApiKey(apiKey)
        .Build();

    var startTime = new DateTimeOffset(DateTime.Parse("11/1/2025"), TimeSpan.Zero);
    var endTime = new DateTimeOffset(DateTime.Parse("11/2/2025"), TimeSpan.Zero);

    Console.WriteLine($"Querying with INVALID symbol 'CL' from {startTime} to {endTime}...");

    await foreach (var record in client.GetRangeAsync(
        dataset: "GLBX.MDP3",
        schema: Schema.Ohlcv1D,
        symbols: ["CL"],  // INVALID - should be CLZ5
        startTime: startTime,
        endTime: endTime))
    {
        Console.WriteLine($"Record: {record}");
    }

    Console.WriteLine("ERROR: Should have thrown exception!");
}
catch (DbentoException ex)
{
    Console.WriteLine($"✅ Caught DbentoException as expected:");
    Console.WriteLine($"   Code: {ex.ErrorCode}");
    Console.WriteLine($"   Message: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Unexpected exception type: {ex.GetType().Name}");
    Console.WriteLine($"   Message: {ex.Message}");
    Console.WriteLine($"   Stack: {ex.StackTrace}");
}
