using Databento.Client.Builders;
using Databento.Client.Historical;
using Databento.Client.Models;

namespace FuturesFilter.Example;

/// <summary>
/// Example showing how to get a list of all parent symbols for a dataset
/// </summary>
public static class GetParentSymbols
{
    public static async Task<HashSet<string>> GetAllParentsAsync(
        IHistoricalClient client,
        string dataset,
        DateTimeOffset date)
    {
        var parentSymbols = new HashSet<string>();

        Console.WriteLine($"Fetching all instrument definitions for {dataset} on {date:yyyy-MM-dd}...");

        // Query ALL_SYMBOLS with definition schema to get all instruments
        await foreach (var record in client.GetRangeAsync(
            dataset: dataset,
            schema: Schema.Definition,
            symbols: new[] { "ALL_SYMBOLS" },
            startTime: date,
            endTime: date.AddDays(1)))
        {
            if (record is InstrumentDefMessage def && !string.IsNullOrEmpty(def.Asset))
            {
                // Add parent symbols based on instrument class
                switch (def.InstrumentClass)
                {
                    case InstrumentClass.Future:
                    case InstrumentClass.FutureSpread:
                        parentSymbols.Add($"{def.Asset}.FUT");
                        break;

                    case InstrumentClass.Call:
                    case InstrumentClass.Put:
                    case InstrumentClass.OptionSpread:
                        parentSymbols.Add($"{def.Asset}.OPT");
                        break;
                }
            }
        }

        return parentSymbols;
    }

    public static async Task RunExampleAsync()
    {
        var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("ERROR: DATABENTO_API_KEY environment variable not set");
            return;
        }

        await using var client = new HistoricalClientBuilder()
            .WithApiKey(apiKey)
            .Build();

        // Example: Get all parent symbols for OPRA.PILLAR (options)
        var date = new DateTimeOffset(2024, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var parents = await GetAllParentsAsync(client, "OPRA.PILLAR", date);

        Console.WriteLine($"\nFound {parents.Count} unique parent symbols:");
        foreach (var parent in parents.OrderBy(p => p).Take(20))
        {
            Console.WriteLine($"  {parent}");
        }

        if (parents.Count > 20)
        {
            Console.WriteLine($"  ... and {parents.Count - 20} more");
        }

        // Example: Get futures parent symbols for GLBX.MDP3
        Console.WriteLine("\n" + new string('-', 50));
        var futuresParents = await GetAllParentsAsync(client, "GLBX.MDP3", date);

        Console.WriteLine($"\nFound {futuresParents.Count} unique futures parent symbols:");
        foreach (var parent in futuresParents.OrderBy(p => p).Take(20))
        {
            Console.WriteLine($"  {parent}");
        }

        if (futuresParents.Count > 20)
        {
            Console.WriteLine($"  ... and {futuresParents.Count - 20} more");
        }
    }
}
