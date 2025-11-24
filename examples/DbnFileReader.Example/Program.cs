using Databento.Client.Builders;
using Databento.Client.Dbn;
using Databento.Client.Models;
using Databento.Client.Models.Dbn;

Console.WriteLine("=== Databento DBN File Reading Example ===");
Console.WriteLine();
Console.WriteLine("This example demonstrates how to read and process DBN files");
Console.WriteLine("using both callback-based (Replay) and blocking (NextRecord) APIs.");
Console.WriteLine();

var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
    ?? throw new InvalidOperationException(
        "DATABENTO_API_KEY environment variable is not set. " +
        "Set it with your API key to authenticate.");

// ============================================================================
// Step 1: Create a Sample DBN File
// ============================================================================

Console.WriteLine("Step 1: Creating Sample DBN File");
Console.WriteLine("---------------------------------");
Console.WriteLine("Downloading a small amount of historical data to demonstrate file reading.");
Console.WriteLine();

var client = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();

string sampleFilePath = Path.Combine(Path.GetTempPath(), "databento_sample.dbn.zst");

try
{
    Console.WriteLine("Requesting data:");
    Console.WriteLine("  Dataset: EQUS.MINI");
    Console.WriteLine("  Symbol: NVDA");
    Console.WriteLine("  Schema: Trades");
    Console.WriteLine("  Date: 2024-01-02, 12:00-12:05 (5 minutes)");
    Console.WriteLine();

    var startTime = new DateTimeOffset(2024, 1, 2, 12, 0, 0, TimeSpan.Zero);
    var endTime = new DateTimeOffset(2024, 1, 2, 12, 5, 0, TimeSpan.Zero);

    var filePath = await client.GetRangeToFileAsync(
        sampleFilePath,
        "EQUS.MINI",
        Schema.Trades,
        new[] { "NVDA" },
        startTime,
        endTime);

    var fileInfo = new FileInfo(filePath);
    Console.WriteLine($"✅ Sample file created: {Path.GetFileName(filePath)}");
    Console.WriteLine($"   Size: {FormatBytes((ulong)fileInfo.Length)}");
    Console.WriteLine($"   Path: {filePath}");
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error creating sample file: {ex.Message}");
    Console.WriteLine("This example requires an active API key and network connection.");
    return;
}

// ============================================================================
// Example 1: Read Metadata
// ============================================================================

Console.WriteLine("Example 1: Read DBN File Metadata");
Console.WriteLine("----------------------------------");
Console.WriteLine("Access metadata without reading all records.");
Console.WriteLine();

try
{
    using var store = new DbnFileStore(sampleFilePath);

    var metadata = store.Metadata;

    Console.WriteLine("File Metadata:");
    Console.WriteLine($"  Version: {metadata.Version}");
    Console.WriteLine($"  Dataset: {metadata.Dataset}");
    Console.WriteLine($"  Schema: {metadata.Schema}");
    Console.WriteLine($"  Start: {metadata.Start} ({DateTimeOffset.FromUnixTimeMilliseconds(metadata.Start / 1_000_000):yyyy-MM-dd HH:mm:ss})");
    Console.WriteLine($"  End: {metadata.End} ({DateTimeOffset.FromUnixTimeMilliseconds(metadata.End / 1_000_000):yyyy-MM-dd HH:mm:ss})");
    Console.WriteLine($"  Limit: {metadata.Limit}");
    Console.WriteLine($"  Stype In: {metadata.StypeIn}");
    Console.WriteLine($"  Stype Out: {metadata.StypeOut}");
    Console.WriteLine($"  Ts Out: {metadata.TsOut}");
    Console.WriteLine($"  Symbol Cstr Len: {metadata.SymbolCstrLen}");
    Console.WriteLine($"  Symbol Count: {metadata.Symbols.Count}");
    Console.WriteLine($"  Symbols: {string.Join(", ", metadata.Symbols)}");

    // Show partial and not_found symbols
    if (metadata.Partial.Count > 0)
    {
        Console.WriteLine($"  Partial Symbols: {string.Join(", ", metadata.Partial)}");
    }
    else
    {
        Console.WriteLine($"  Partial Symbols: (none)");
    }

    if (metadata.NotFound.Count > 0)
    {
        Console.WriteLine($"  Not Found Symbols: {string.Join(", ", metadata.NotFound)}");
    }
    else
    {
        Console.WriteLine($"  Not Found Symbols: (none)");
    }

    // Show symbol mappings
    if (metadata.Mappings.Count > 0)
    {
        Console.WriteLine($"  Symbol Mappings ({metadata.Mappings.Count}):");
        foreach (var mapping in metadata.Mappings)
        {
            Console.WriteLine($"    {mapping.RawSymbol}:");
            foreach (var interval in mapping.Intervals)
            {
                Console.WriteLine($"      {interval.StartDate} to {interval.EndDate}: {interval.Symbol}");
            }
        }
    }
    else
    {
        Console.WriteLine($"  Symbol Mappings: (none)");
    }

    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error reading metadata: {ex.Message}");
    Console.WriteLine();
}

// ============================================================================
// Example 2: Replay with Record Callback Only
// ============================================================================

Console.WriteLine("Example 2: Replay with Record Callback");
Console.WriteLine("---------------------------------------");
Console.WriteLine("Process all records using a callback (simplest form).");
Console.WriteLine();

try
{
    using var store = new DbnFileStore(sampleFilePath);

    int recordCount = 0;
    long totalSize = 0;

    bool ProcessRecord(Record record)
    {
        recordCount++;

        // Example: Cast to TradeMessage if it's a trade
        if (record is TradeMessage tradeMsg)
        {
            totalSize += (long)tradeMsg.Size;

            // Show first 10 trades as examples with full details
            if (recordCount <= 10)
            {
                Console.WriteLine($"  Trade #{recordCount}:");
                Console.WriteLine($"    Timestamp:     {tradeMsg.Timestamp:yyyy-MM-dd HH:mm:ss.ffffff} UTC");
                Console.WriteLine($"    Instrument ID: {tradeMsg.InstrumentId}");
                Console.WriteLine($"    Price:         ${tradeMsg.PriceDecimal:F4} (raw: {tradeMsg.Price})");
                Console.WriteLine($"    Size:          {tradeMsg.Size:N0} shares");
                Console.WriteLine($"    Side:          {tradeMsg.Side}");
                Console.WriteLine($"    Action:        {tradeMsg.Action}");
                Console.WriteLine($"    Flags:         {tradeMsg.Flags}");
                Console.WriteLine($"    Sequence:      {tradeMsg.Sequence}");
                Console.WriteLine();
            }
            else if (recordCount == 11)
            {
                Console.WriteLine($"  ... (showing first 10 trades, continuing to process remaining records)");
                Console.WriteLine();
            }
        }

        return true; // Continue processing
    }

    store.Replay(ProcessRecord);

    Console.WriteLine($"✅ Processed {recordCount:N0} trade(s)");
    Console.WriteLine($"   Total volume: {totalSize:N0} shares");
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error replaying records: {ex.Message}");
    Console.WriteLine();
}

// ============================================================================
// Example 3: Replay with Metadata and Record Callbacks
// ============================================================================

Console.WriteLine("Example 3: Replay with Metadata Callback");
Console.WriteLine("-----------------------------------------");
Console.WriteLine("Process metadata and records with separate callbacks.");
Console.WriteLine();

try
{
    using var store = new DbnFileStore(sampleFilePath);

    void MetadataCallback(DbnMetadata metadata)
    {
        Console.WriteLine("Metadata Callback Invoked:");
        Console.WriteLine($"  Processing {metadata.Dataset} data");
        Console.WriteLine($"  Schema: {metadata.Schema}");
        Console.WriteLine($"  Time range: {metadata.Start} to {metadata.End}");
        Console.WriteLine();
    }

    int largeTradeCount = 0;

    bool RecordCallback(Record record)
    {
        // Filter for large trades (size > 100 shares)
        if (record is TradeMessage tradeMsg && tradeMsg.Size > 100)
        {
            largeTradeCount++;

            if (largeTradeCount <= 10)
            {
                var value = tradeMsg.PriceDecimal * tradeMsg.Size;
                Console.WriteLine($"  Large Trade #{largeTradeCount}:");
                Console.WriteLine($"    Time:          {tradeMsg.Timestamp:HH:mm:ss.ffffff}");
                Console.WriteLine($"    Size:          {tradeMsg.Size:N0} shares");
                Console.WriteLine($"    Price:         ${tradeMsg.PriceDecimal:F4}");
                Console.WriteLine($"    Notional:      ${value:N2}");
                Console.WriteLine($"    Side:          {tradeMsg.Side}");
                Console.WriteLine();
            }
            else if (largeTradeCount == 11)
            {
                Console.WriteLine($"  ... (showing first 10 large trades, continuing to count remaining)");
                Console.WriteLine();
            }
        }

        return true; // Continue
    }

    store.Replay(MetadataCallback, RecordCallback);

    Console.WriteLine($"✅ Found {largeTradeCount:N0} large trade(s) (size > 100)");
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error replaying with callbacks: {ex.Message}");
    Console.WriteLine();
}

// ============================================================================
// Example 4: Early Termination with Replay
// ============================================================================

Console.WriteLine("Example 4: Early Termination");
Console.WriteLine("-----------------------------");
Console.WriteLine("Stop processing after finding specific records.");
Console.WriteLine();

try
{
    using var store = new DbnFileStore(sampleFilePath);

    int processedCount = 0;
    const int maxRecords = 20;

    bool ProcessRecordWithLimit(Record record)
    {
        processedCount++;

        if (processedCount <= 10)
        {
            Console.WriteLine($"  Record #{processedCount}: {record.GetType().Name}");

            // Show key details for each record type
            if (record is TradeMessage trade)
            {
                Console.WriteLine($"    → {trade.Timestamp:HH:mm:ss.fff} | ${trade.PriceDecimal:F2} x {trade.Size} | {trade.Side}");
            }
        }
        else if (processedCount == 11)
        {
            Console.WriteLine($"  ... (continuing to process up to {maxRecords} records)");
        }

        // Stop after processing maxRecords
        if (processedCount >= maxRecords)
        {
            Console.WriteLine($"  ... (stopped after {maxRecords} records)");
            return false; // Stop processing
        }

        return true; // Continue
    }

    store.Replay(ProcessRecordWithLimit);

    Console.WriteLine();
    Console.WriteLine($"✅ Processed {processedCount} records before stopping");
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error during replay: {ex.Message}");
    Console.WriteLine();
}

// ============================================================================
// Example 5: Blocking API with NextRecord
// ============================================================================

Console.WriteLine("Example 5: Blocking API (NextRecord)");
Console.WriteLine("-------------------------------------");
Console.WriteLine("Iterate through records manually using NextRecord().");
Console.WriteLine();

try
{
    using var store = new DbnFileStore(sampleFilePath);

    Console.WriteLine($"File contains: {store.Metadata.Schema} data");
    Console.WriteLine();

    int count = 0;
    decimal minPrice = decimal.MaxValue;
    decimal maxPrice = decimal.MinValue;
    decimal totalVolume = 0;
    decimal totalNotional = 0;
    var priceTimeSeries = new List<(DateTimeOffset Time, decimal Price, uint Size)>();

    // Manual iteration
    while (true)
    {
        var record = store.NextRecord();
        if (record == null)
            break; // End of file

        count++;

        // Track price range for trades
        if (record is TradeMessage tradeMsg)
        {
            decimal price = tradeMsg.PriceDecimal;
            minPrice = Math.Min(minPrice, price);
            maxPrice = Math.Max(maxPrice, price);
            totalVolume += tradeMsg.Size;
            totalNotional += price * tradeMsg.Size;

            priceTimeSeries.Add((tradeMsg.Timestamp, price, tradeMsg.Size));
        }

        // Show first 10 records with details
        if (count <= 10 && record is TradeMessage trade)
        {
            Console.WriteLine($"  Record #{count}: {record.GetType().Name}");
            Console.WriteLine($"    → {trade.Timestamp:HH:mm:ss.ffffff} | ${trade.PriceDecimal:F4} x {trade.Size} | {trade.Side}");
        }
        else if (count == 11)
        {
            Console.WriteLine($"  ... (continuing to read remaining records)");
        }
    }

    Console.WriteLine($"  ... (read {count:N0} total records)");
    Console.WriteLine();

    if (minPrice != decimal.MaxValue)
    {
        var avgPrice = priceTimeSeries.Any()
            ? priceTimeSeries.Average(x => x.Price)
            : 0;
        var vwap = totalVolume > 0
            ? totalNotional / totalVolume
            : 0;

        Console.WriteLine("Trade Statistics:");
        Console.WriteLine($"  Total Trades:      {count:N0}");
        Console.WriteLine($"  Total Volume:      {totalVolume:N0} shares");
        Console.WriteLine($"  Total Notional:    ${totalNotional:N2}");
        Console.WriteLine();
        Console.WriteLine("Price Statistics:");
        Console.WriteLine($"  Min Price:         ${minPrice:F4}");
        Console.WriteLine($"  Max Price:         ${maxPrice:F4}");
        Console.WriteLine($"  Price Range:       ${maxPrice - minPrice:F4}");
        Console.WriteLine($"  Average Price:     ${avgPrice:F4}");
        Console.WriteLine($"  VWAP:              ${vwap:F4}");

        if (priceTimeSeries.Count >= 2)
        {
            var firstTrade = priceTimeSeries.First();
            var lastTrade = priceTimeSeries.Last();
            var priceChange = lastTrade.Price - firstTrade.Price;
            var priceChangePct = (priceChange / firstTrade.Price) * 100;

            Console.WriteLine();
            Console.WriteLine("Time Period:");
            Console.WriteLine($"  First Trade:       {firstTrade.Time:HH:mm:ss.ffffff} @ ${firstTrade.Price:F4}");
            Console.WriteLine($"  Last Trade:        {lastTrade.Time:HH:mm:ss.ffffff} @ ${lastTrade.Price:F4}");
            Console.WriteLine($"  Price Change:      ${priceChange:F4} ({priceChangePct:+0.00;-0.00}%)");
        }
    }

    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error using NextRecord: {ex.Message}");
    Console.WriteLine();
}

// ============================================================================
// Example 6: Reset and Re-read
// ============================================================================

Console.WriteLine("Example 6: Reset and Re-read");
Console.WriteLine("-----------------------------");
Console.WriteLine("Reset the reader to start from the beginning.");
Console.WriteLine();

try
{
    using var store = new DbnFileStore(sampleFilePath);

    // First pass: count records
    int firstPassCount = 0;
    while (store.NextRecord() != null)
    {
        firstPassCount++;
    }

    Console.WriteLine($"First pass: Read {firstPassCount} records");

    // Reset
    store.Reset();
    Console.WriteLine("Reset complete - back to start of file");

    // Second pass: count again
    int secondPassCount = 0;
    while (store.NextRecord() != null)
    {
        secondPassCount++;
    }

    Console.WriteLine($"Second pass: Read {secondPassCount} records");
    Console.WriteLine();

    if (firstPassCount == secondPassCount)
    {
        Console.WriteLine($"✅ Reset successful - both passes read same count");
    }

    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error during reset: {ex.Message}");
    Console.WriteLine();
}

// ============================================================================
// Example 7: Compressed File Support
// ============================================================================

Console.WriteLine("Example 7: Compressed File Support");
Console.WriteLine("-----------------------------------");
Console.WriteLine("DbnFileStore automatically handles .dbn.zst compressed files.");
Console.WriteLine();

try
{
    var fileInfo = new FileInfo(sampleFilePath);
    bool isCompressed = sampleFilePath.EndsWith(".zst", StringComparison.OrdinalIgnoreCase);

    Console.WriteLine($"File: {Path.GetFileName(sampleFilePath)}");
    Console.WriteLine($"Size: {FormatBytes((ulong)fileInfo.Length)}");
    Console.WriteLine($"Compressed: {(isCompressed ? "Yes (.zst)" : "No (.dbn)")}");
    Console.WriteLine();

    using var store = new DbnFileStore(sampleFilePath);
    int recordCount = 0;

    store.Replay(record =>
    {
        recordCount++;
        return true;
    });

    Console.WriteLine($"✅ Successfully read {recordCount:N0} records from {(isCompressed ? "compressed" : "uncompressed")} file");
    Console.WriteLine();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error reading compressed file: {ex.Message}");
    Console.WriteLine();
}

// ============================================================================
// Cleanup
// ============================================================================

Console.WriteLine("Cleanup");
Console.WriteLine("-------");
try
{
    if (File.Exists(sampleFilePath))
    {
        File.Delete(sampleFilePath);
        Console.WriteLine($"✅ Deleted sample file: {Path.GetFileName(sampleFilePath)}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️  Could not delete sample file: {ex.Message}");
}
Console.WriteLine();

// ============================================================================
// Summary
// ============================================================================

Console.WriteLine("=== DBN File Reading Summary ===");
Console.WriteLine();
Console.WriteLine("DbnFileStore provides two APIs:");
Console.WriteLine();
Console.WriteLine("1. Callback API (Replay):");
Console.WriteLine("   • Replay(recordCallback) - Simple callback for each record");
Console.WriteLine("   • Replay(metadataCallback, recordCallback) - Separate callbacks");
Console.WriteLine("   • Return true to continue, false to stop");
Console.WriteLine("   • Similar to TimeseriesGetRange and LiveThreaded");
Console.WriteLine();
Console.WriteLine("2. Blocking API (NextRecord):");
Console.WriteLine("   • NextRecord() - Manually iterate records");
Console.WriteLine("   • Reset() - Return to beginning of file");
Console.WriteLine("   • Similar to LiveBlocking");
Console.WriteLine();
Console.WriteLine("Key Features:");
Console.WriteLine("   • Automatic compression support (.dbn.zst)");
Console.WriteLine("   • Lazy metadata loading");
Console.WriteLine("   • Memory efficient streaming");
Console.WriteLine("   • Type-safe record casting");
Console.WriteLine("   • Early termination support");
Console.WriteLine();
Console.WriteLine("Best Practices:");
Console.WriteLine("   • Use Replay for full file processing");
Console.WriteLine("   • Use NextRecord for manual control");
Console.WriteLine("   • Wrap in using statement for proper disposal");
Console.WriteLine("   • Copy record data if needed beyond callback scope");
Console.WriteLine();
Console.WriteLine("=== DBN File Reader Example Complete ===");

// Helper function
static string FormatBytes(ulong bytes)
{
    string[] sizes = ["B", "KB", "MB", "GB", "TB"];
    double len = bytes;
    int order = 0;
    while (len >= 1024 && order < sizes.Length - 1)
    {
        order++;
        len /= 1024;
    }
    return $"{len:0.##} {sizes[order]}";
}
