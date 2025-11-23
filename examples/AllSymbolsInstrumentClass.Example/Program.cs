using Databento.Client.Builders;
using Databento.Client.Historical;
using Databento.Client.Models;

namespace AllSymbolsInstrumentClass.Example;

/// <summary>
/// Tests querying ALL_SYMBOLS for instrument definitions and displays InstrumentClass distribution.
///
/// This example demonstrates:
/// - Using ALL_SYMBOLS to get all instruments in a dataset
/// - InstrumentClass is now correctly populated (v4.0.0 fix)
/// - Distribution of instrument types across the entire dataset
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("ALL_SYMBOLS InstrumentClass Test (v4.0.0)");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        // Get API key from environment
        string? apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: DATABENTO_API_KEY environment variable not set.");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Please set your API key:");
            Console.WriteLine("  Windows: setx DATABENTO_API_KEY \"your-api-key\"");
            Console.WriteLine("  Linux/Mac: export DATABENTO_API_KEY=\"your-api-key\"");
            return;
        }

        try
        {
            var client = new HistoricalClientBuilder()
                .WithApiKey(apiKey)
                .Build();
            Console.WriteLine("✓ Created HistoricalClient");
            Console.WriteLine();

            // Query configuration
            string dataset = "GLBX.MDP3";
            var endTime = new DateTimeOffset(2023, 11, 15, 0, 0, 0, TimeSpan.Zero);
            var startTime = endTime.AddDays(-1);  // Just 1 day to limit data
            int recordLimit = 100;  // Limit records to avoid long wait

            Console.WriteLine("Query Parameters:");
            Console.WriteLine($"  Dataset:      {dataset}");
            Console.WriteLine($"  Symbols:      ALL_SYMBOLS");
            Console.WriteLine($"  Schema:       Definition");
            Console.WriteLine($"  Start Time:   {startTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  End Time:     {endTime:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"  Record Limit: {recordLimit}");
            Console.WriteLine();

            Console.WriteLine("Querying instrument definitions for ALL_SYMBOLS...");
            Console.WriteLine();

            // Track InstrumentClass distribution
            var classDistribution = new Dictionary<InstrumentClass, int>();
            int totalCount = 0;
            int displayedCount = 0;
            const int displayLimit = 20;  // Only display first 20

            await foreach (var record in client.GetRangeAsync(
                dataset: dataset,
                schema: Schema.Definition,
                symbols: new[] { "ALL_SYMBOLS" },
                startTime: startTime,
                endTime: endTime))
            {
                if (record is InstrumentDefMessage def)
                {
                    // Track distribution
                    if (!classDistribution.ContainsKey(def.InstrumentClass))
                    {
                        classDistribution[def.InstrumentClass] = 0;
                    }
                    classDistribution[def.InstrumentClass]++;
                    totalCount++;

                    // Display first few
                    if (displayedCount < displayLimit)
                    {
                        Console.WriteLine($"  [{totalCount,3}] {def.RawSymbol,-25} InstrumentClass: {def.InstrumentClass}");
                        displayedCount++;
                    }
                    else if (displayedCount == displayLimit)
                    {
                        Console.WriteLine($"  ... (showing first {displayLimit}, continuing to collect data)");
                        displayedCount++;
                    }
                }

                // Limit total records
                if (totalCount >= recordLimit)
                {
                    break;
                }
            }

            Console.WriteLine();
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("RESULTS");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            Console.WriteLine($"Total Instruments Processed: {totalCount}");
            Console.WriteLine();

            Console.WriteLine("InstrumentClass Distribution:");
            foreach (var kvp in classDistribution.OrderByDescending(x => x.Value))
            {
                double percentage = (kvp.Value * 100.0) / totalCount;
                Console.WriteLine($"  {kvp.Key,-20} {kvp.Value,4} instruments ({percentage:F1}%)");
            }
            Console.WriteLine();

            // Verify fix
            int unknownCount = classDistribution.GetValueOrDefault(InstrumentClass.Unknown, 0);
            int validCount = totalCount - unknownCount;

            if (validCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ SUCCESS: v4.0.0 fix is working!");
                Console.ResetColor();
                Console.WriteLine($"  {validCount} instruments have valid InstrumentClass values");
                Console.WriteLine($"  {unknownCount} instruments are Unknown (this is normal for undefined types)");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ FAILURE: All instruments have InstrumentClass = Unknown");
                Console.ResetColor();
                Console.WriteLine("  This indicates the v4.0.0 fix is not working correctly.");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("Common issues:");
            Console.WriteLine("  - Invalid API key");
            Console.WriteLine("  - No data available for date range");
            Console.WriteLine("  - Dataset requires special permissions");
            Console.WriteLine("  - Network connectivity issues");
            Console.WriteLine();
            Console.WriteLine("Full exception:");
            Console.WriteLine(ex);
        }
    }
}
