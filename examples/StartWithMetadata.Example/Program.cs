using Databento.Client.Builders;
using Databento.Client.Models;
using Databento.Interop;

namespace StartWithMetadata.Example;

/// <summary>
/// Demonstrates LiveBlocking::Start returning DBN metadata matching the C++ example:
///
/// auto metadata = client.Start();
/// std::cout << "DBN metadata version "
///           << static_cast<std::uint16_t>(metadata.version)
///           << " and dataset " << metadata.dataset << '\n';
///
/// Start() instructs the live gateway to start sending data and returns DBN metadata
/// about the subscription(s). This metadata includes:
/// - version: DBN schema version number
/// - dataset: Dataset code (e.g., "EQUS.MINI", "GLBX.MDP3")
/// - schema: Data record schema (null for mixed schemas)
/// - start/end timestamps
/// - symbols, partial, not_found lists
/// - symbol mappings
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Databento Start with Metadata Example (LiveBlocking)");
        Console.WriteLine("====================================================\n");

        // Get API key from environment variable
        var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("ERROR: DATABENTO_API_KEY environment variable is not set.");
            Console.WriteLine("Please set your API key and try again.");
            return;
        }

        Console.WriteLine("✓ API key found");
        Console.WriteLine();

        try
        {
            // Build the LiveBlocking client (pull-based API)
            await using var client = new LiveBlockingClientBuilder()
                .WithApiKey(apiKey)
                .WithDataset("EQUS.MINI")
                .Build();

            Console.WriteLine("✓ Created LiveBlockingClient with dataset: EQUS.MINI");
            Console.WriteLine();

            // Subscribe to NVDA trades
            await client.SubscribeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Trades,
                symbols: new[] { "NVDA" }
            );

            Console.WriteLine("✓ Subscribed to NVDA trades");
            Console.WriteLine();

            // ===================================================================
            // START WITH METADATA - This is the key feature!
            // The C++ client.Start() blocks and returns metadata
            // Our async version awaits and returns metadata
            // This matches: auto metadata = client.Start();
            // ===================================================================

            Console.WriteLine("Starting live stream and waiting for metadata...");
            Console.WriteLine();

            var metadata = await client.StartAsync();

            // ===================================================================
            // DISPLAY METADATA - Matching the C++ output pattern:
            // std::cout << "DBN metadata version "
            //           << static_cast<std::uint16_t>(metadata.version)
            //           << " and dataset " << metadata.dataset << '\n';
            // ===================================================================

            Console.WriteLine("✓ Start completed and returned DBN metadata!");
            Console.WriteLine();
            Console.WriteLine($"DBN metadata version {metadata.Version} and dataset {metadata.Dataset}");
            Console.WriteLine();

            // Show additional metadata details
            Console.WriteLine("Additional Metadata Details:");
            Console.WriteLine("----------------------------");
            Console.WriteLine($"Schema:         {metadata.Schema?.ToString() ?? "(mixed)"}");
            Console.WriteLine($"Stype Out:      {metadata.StypeOut}");
            Console.WriteLine($"Timestamp Out:  {metadata.TsOut}");
            Console.WriteLine($"Start Time:     {DateTimeOffset.FromUnixTimeMilliseconds(metadata.Start / 1_000_000):yyyy-MM-dd HH:mm:ss} UTC");
            if (metadata.End > 0)
                Console.WriteLine($"End Time:       {DateTimeOffset.FromUnixTimeMilliseconds(metadata.End / 1_000_000):yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"Symbols:        {string.Join(", ", metadata.Symbols)}");
            if (metadata.Partial.Count > 0)
                Console.WriteLine($"Partial:        {string.Join(", ", metadata.Partial)}");
            if (metadata.NotFound.Count > 0)
                Console.WriteLine($"Not Found:      {string.Join(", ", metadata.NotFound)}");
            Console.WriteLine();

            Console.WriteLine("Now pulling records for 5 seconds using NextRecordAsync...");
            Console.WriteLine();

            // Track messages
            var recordCount = 0;
            var tradeCount = 0;
            var startTime = DateTime.Now;

            // Pull records for 5 seconds
            while ((DateTime.Now - startTime).TotalSeconds < 5)
            {
                var record = await client.NextRecordAsync(timeout: TimeSpan.FromSeconds(1));

                if (record == null)
                {
                    Console.WriteLine("[Timeout - no record received in 1 second]");
                    continue;
                }

                recordCount++;

                if (record is TradeMessage trade)
                {
                    tradeCount++;
                    if (tradeCount <= 3)
                    {
                        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(trade.TimestampNs / 1_000_000);
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Trade #{tradeCount}:");
                        Console.WriteLine($"  Price: ${trade.PriceDecimal:F2}");
                        Console.WriteLine($"  Size:  {trade.Size}");
                        Console.WriteLine($"  Time:  {timestamp:HH:mm:ss.fff} UTC");
                        Console.WriteLine();
                    }
                }
                else if (record is SystemMessage sysMsg)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {sysMsg}");
                    Console.WriteLine();
                }
            }

            // Stop streaming
            await client.StopAsync();

            Console.WriteLine();
            Console.WriteLine("Start with metadata example completed!");
            Console.WriteLine();
            Console.WriteLine("Summary:");
            Console.WriteLine($"  Total records:  {recordCount}");
            Console.WriteLine($"  Trade messages: {tradeCount}");
            Console.WriteLine();

            Console.WriteLine("API Mapping:");
            Console.WriteLine("  C++: auto metadata = client.Start();");
            Console.WriteLine("       std::cout << \"DBN metadata version \" << static_cast<std::uint16_t>(metadata.version)");
            Console.WriteLine("                 << \" and dataset \" << metadata.dataset << '\\n';");
            Console.WriteLine();
            Console.WriteLine("  C#:  var metadata = await client.StartAsync();");
            Console.WriteLine("       Console.WriteLine($\"DBN metadata version {metadata.Version} and dataset {metadata.Dataset}\");");
            Console.WriteLine();
            Console.WriteLine("       // Pull-based record retrieval:");
            Console.WriteLine("       var record = await client.NextRecordAsync();");
            Console.WriteLine();

            Console.WriteLine("Key Features Demonstrated:");
            Console.WriteLine("--------------------------");
            Console.WriteLine("✓ Start() returns DBN metadata (matches C++ LiveBlocking::Start)");
            Console.WriteLine("✓ Metadata contains version (byte) and dataset (string) fields");
            Console.WriteLine("✓ Additional metadata: schema, timestamps, symbols, mappings");
            Console.WriteLine("✓ Metadata received before data stream begins");
            Console.WriteLine("✓ Pull-based record retrieval via NextRecordAsync()");
        }
        catch (DbentoAuthenticationException ex)
        {
            Console.WriteLine($"✗ Authentication failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.GetType().Name}");
            Console.WriteLine($"  Message: {ex.Message}");
            if (ex.StackTrace != null)
            {
                Console.WriteLine($"  Stack: {ex.StackTrace}");
            }
        }
    }
}
