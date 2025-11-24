#!/usr/bin/env dotnet-script
#r "src/Databento.Client/bin/Release/net8.0/Databento.Client.dll"
#r "src/Databento.Interop/bin/Release/net8.0/Databento.Interop.dll"

using Databento.Client.Builders;
using Databento.Interop;
using Databento.Client.Models;

Console.WriteLine("Testing VALID symbol to ensure basic flow works...");

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

    Console.WriteLine($"Querying with VALID symbol 'CLZ5' from {startTime} to {endTime}...");

    int count = 0;
    await foreach (var record in client.GetRangeAsync(
        dataset: "GLBX.MDP3",
        schema: Schema.Ohlcv1D,
        symbols: ["CLZ5"],  // VALID symbol
        startTime: startTime,
        endTime: endTime))
    {
        count++;
        if (count <= 3)
        {
            Console.WriteLine($"Record {count}: {record}");
        }
    }

    Console.WriteLine($"✅ Successfully retrieved {count} records with valid symbol!");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Unexpected exception: {ex.GetType().Name}");
    Console.WriteLine($"   Message: {ex.Message}");
}
