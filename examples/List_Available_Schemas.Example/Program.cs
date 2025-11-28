using Databento.Client.Builders;
using Databento.Client.Models;

Console.WriteLine("================================================================================");
Console.WriteLine("List Available Schemas Example");
Console.WriteLine("================================================================================");
Console.WriteLine();
Console.WriteLine("This example demonstrates Historical::MetadataListSchemas from databento-cpp");
Console.WriteLine("Mapped to: HistoricalClient.ListSchemasAsync(dataset) in .NET");
Console.WriteLine();

// Get API key from environment
var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("ERROR: DATABENTO_API_KEY environment variable not set!");
    Console.WriteLine();
    Console.WriteLine("Set your API key:");
    Console.WriteLine("  Windows: setx DATABENTO_API_KEY \"your-key\"");
    Console.WriteLine("  Linux/Mac: export DATABENTO_API_KEY=\"your-key\"");
    return 1;
}

// Create Historical client
await using var client = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();

Console.WriteLine("✓ Created HistoricalClient");
Console.WriteLine();

// Define datasets to query
var datasets = new[] { "EQUS.MINI", "GLBX.MDP3", "XNAS.ITCH" };

Console.WriteLine($"Querying schema availability for {datasets.Length} datasets...");
Console.WriteLine();

// Store all schemas across datasets
var allSchemas = new Dictionary<string, List<Schema>>();

foreach (var dataset in datasets)
{
    try
    {
        Console.WriteLine($"Dataset: {dataset}");
        Console.WriteLine(new string('-', 80));

        // Call MetadataListSchemas (via ListSchemasAsync)
        var schemas = await client.ListSchemasAsync(dataset);
        allSchemas[dataset] = schemas.ToList();

        Console.WriteLine($"Available schemas: {schemas.Count}");
        Console.WriteLine();

        // Categorize schemas
        var timeSeriesSchemas = new List<Schema>();
        var metadataSchemas = new List<Schema>();

        foreach (var schema in schemas)
        {
            // Categorize based on schema type
            if (schema == Schema.Definition || schema == Schema.Statistics || schema == Schema.Status || schema == Schema.Imbalance)
            {
                metadataSchemas.Add(schema);
            }
            else
            {
                timeSeriesSchemas.Add(schema);
            }
        }

        // Display time-series schemas
        if (timeSeriesSchemas.Count > 0)
        {
            Console.WriteLine("  Time-Series Schemas (queryable via TimeseriesGetRange):");
            foreach (var schema in timeSeriesSchemas)
            {
                var description = GetSchemaDescription(schema);
                Console.WriteLine($"    • {schema,-12} - {description}");
            }
            Console.WriteLine();
        }

        // Display metadata schemas
        if (metadataSchemas.Count > 0)
        {
            Console.WriteLine("  Metadata Schemas (point-in-time data):");
            foreach (var schema in metadataSchemas)
            {
                var description = GetSchemaDescription(schema);
                Console.WriteLine($"    • {schema,-12} - {description}");
            }
            Console.WriteLine();
        }

        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ✗ Error: {ex.Message}");
        Console.WriteLine();
    }
}

// Summary
Console.WriteLine("================================================================================");
Console.WriteLine("SCHEMA OVERVIEW");
Console.WriteLine("================================================================================");
Console.WriteLine();

Console.WriteLine("Schema Categories:");
Console.WriteLine();

Console.WriteLine("1. ORDER BOOK SCHEMAS (Time-Series)");
Console.WriteLine("   • MBO    - Market By Order (individual orders)");
Console.WriteLine("   • MBP-1  - Market By Price, 1 level (best bid/ask)");
Console.WriteLine("   • MBP-10 - Market By Price, 10 levels");
Console.WriteLine("   • TBBO   - Top of Book Best Bid/Offer");
Console.WriteLine("   • CBBO   - Consolidated Best Bid/Offer");
Console.WriteLine();

Console.WriteLine("2. TRADE SCHEMAS (Time-Series)");
Console.WriteLine("   • Trades - Individual trade transactions");
Console.WriteLine();

Console.WriteLine("3. BAR SCHEMAS (Time-Series)");
Console.WriteLine("   • OHLCV-1S  - 1-second OHLCV bars");
Console.WriteLine("   • OHLCV-1M  - 1-minute OHLCV bars");
Console.WriteLine("   • OHLCV-1H  - 1-hour OHLCV bars");
Console.WriteLine("   • OHLCV-1D  - 1-day OHLCV bars");
Console.WriteLine("   • OHLCV-EOD - End-of-day OHLCV bars");
Console.WriteLine("   • BBO-1S    - 1-second BBO snapshots");
Console.WriteLine("   • BBO-1M    - 1-minute BBO snapshots");
Console.WriteLine();

Console.WriteLine("4. METADATA SCHEMAS (Point-in-Time)");
Console.WriteLine("   • Definition - Instrument definitions (static metadata)");
Console.WriteLine("   • Statistics - Market statistics and summary data");
Console.WriteLine("   • Status     - Trading status events");
Console.WriteLine("   • Imbalance  - Auction imbalance data");
Console.WriteLine();

Console.WriteLine("================================================================================");
Console.WriteLine("USAGE GUIDE");
Console.WriteLine("================================================================================");
Console.WriteLine();

Console.WriteLine("Time-Series Schemas:");
Console.WriteLine("  Can be queried with time ranges via GetRangeAsync()");
Console.WriteLine("  Example:");
Console.WriteLine("    await foreach (var record in client.GetRangeAsync(");
Console.WriteLine("        dataset: \"EQUS.MINI\",");
Console.WriteLine("        schema: Schema.Trades,");
Console.WriteLine("        symbols: new[] { \"NVDA\" },");
Console.WriteLine("        startTime: start,");
Console.WriteLine("        endTime: end))");
Console.WriteLine();

Console.WriteLine("Metadata Schemas:");
Console.WriteLine("  Provide point-in-time instrument information");
Console.WriteLine("  Definition schema shows instrument details (not time-series)");
Console.WriteLine("  Use for building symbol universes and instrument lookups");
Console.WriteLine();

Console.WriteLine("================================================================================");
Console.WriteLine("API MAPPING");
Console.WriteLine("================================================================================");
Console.WriteLine();
Console.WriteLine("C++ (databento-cpp):");
Console.WriteLine("  auto schemas = client.MetadataListSchemas(\"EQUS.MINI\");");
Console.WriteLine("  // Returns: std::vector<databento::Schema>");
Console.WriteLine();
Console.WriteLine("C# (.NET):");
Console.WriteLine("  var schemas = await client.ListSchemasAsync(\"EQUS.MINI\");");
Console.WriteLine("  // Returns: Task<IReadOnlyList<Schema>>");
Console.WriteLine();
Console.WriteLine("Parameters:");
Console.WriteLine("  • dataset (string) - Must be from MetadataListDatasets");
Console.WriteLine("                       Constants in databento::dataset namespace");
Console.WriteLine();
Console.WriteLine("Returns:");
Console.WriteLine("  • Vector/List of Schema enums representing available schemas");
Console.WriteLine();

// Display schema availability matrix
if (allSchemas.Count > 0)
{
    Console.WriteLine("================================================================================");
    Console.WriteLine("SCHEMA AVAILABILITY MATRIX");
    Console.WriteLine("================================================================================");
    Console.WriteLine();

    // Get all unique schemas across all datasets
    var uniqueSchemas = allSchemas.Values
        .SelectMany(s => s)
        .Distinct()
        .OrderBy(s => s.ToString())
        .ToList();

    // Print header
    Console.Write("Schema".PadRight(15));
    foreach (var dataset in allSchemas.Keys)
    {
        Console.Write($"{dataset.PadRight(15)}");
    }
    Console.WriteLine();
    Console.WriteLine(new string('-', 15 + (allSchemas.Keys.Count * 15)));

    // Print matrix
    foreach (var schema in uniqueSchemas)
    {
        Console.Write(schema.ToString().PadRight(15));
        foreach (var dataset in allSchemas.Keys)
        {
            var hasSchema = allSchemas[dataset].Contains(schema);
            Console.Write($"{(hasSchema ? "✓" : "—").PadRight(15)}");
        }
        Console.WriteLine();
    }
    Console.WriteLine();
}

Console.WriteLine("=== List Available Schemas Example Complete ===");
Console.WriteLine();

return 0;

// Helper function to get schema descriptions
static string GetSchemaDescription(Schema schema)
{
    return schema switch
    {
        Schema.Mbo => "Market By Order (individual orders)",
        Schema.Mbp1 => "Market By Price, 1 level",
        Schema.Mbp10 => "Market By Price, 10 levels",
        Schema.Tbbo => "Top of Book Best Bid/Offer",
        Schema.Trades => "Individual trade transactions",
        Schema.Ohlcv1S => "1-second OHLCV bars",
        Schema.Ohlcv1M => "1-minute OHLCV bars",
        Schema.Ohlcv1H => "1-hour OHLCV bars",
        Schema.Ohlcv1D => "1-day OHLCV bars",
        Schema.OhlcvEod => "End-of-day OHLCV bars",
        Schema.Definition => "Instrument definitions",
        Schema.Statistics => "Market statistics",
        Schema.Status => "Trading status events",
        Schema.Imbalance => "Auction imbalance data",
        Schema.Bbo1S => "1-second BBO snapshots",
        Schema.Bbo1M => "1-minute BBO snapshots",
        Schema.Cmbp1 => "Consolidated MBP, 1 level",
        Schema.Tcbbo => "Consolidated TBBO",
        Schema.Cbbo1S => "1-second consolidated BBO",
        Schema.Cbbo1M => "1-minute consolidated BBO",
        _ => schema.ToString()
    };
}
