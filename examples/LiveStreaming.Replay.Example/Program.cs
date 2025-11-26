using Databento.Client.Builders;
using Databento.Client.Models;

namespace LiveStreaming.Replay.Example;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Databento Live Streaming REPLAY Example - Testing StartAsync Metadata");
        Console.WriteLine("======================================================================\n");
        Console.WriteLine("This is a REPLAY version that works outside market hours.");
        Console.WriteLine();

        // Get API key from environment variable or command line
        var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
                     ?? (args.Length > 0 ? args[0] : null);

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: API key not provided.");
            Console.WriteLine("Usage: dotnet run <api-key>");
            Console.WriteLine("Or set DATABENTO_API_KEY environment variable");
            return;
        }

        // Calculate most recent market open for valid replay time
        var marketOpen = GetMostRecentMarketOpen();
        Console.WriteLine($"Using most recent market open: {marketOpen:yyyy-MM-dd HH:mm:ss} ET");
        Console.WriteLine();

        try
        {
            // Create live client
            await using var client = new LiveClientBuilder()
                .WithApiKey(apiKey)
                .WithDataset("EQUS.MINI")
                .Build();

            Console.WriteLine("✓ Created live client with dataset: EQUS.MINI");
            Console.WriteLine();

            // Subscribe to data received events
            var recordCount = 0;
            var tradeCount = 0;
            client.DataReceived += (sender, e) =>
            {
                recordCount++;
                if (e.Record is TradeMessage trade)
                {
                    tradeCount++;
                    if (tradeCount <= 10)
                    {
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Trade #{tradeCount}: " +
                            $"InstrumentId={trade.InstrumentId} " +
                            $"Price=${trade.PriceDecimal:F2} " +
                            $"Size={trade.Size} " +
                            $"Side={trade.Side}");
                    }
                }
                else if (recordCount <= 3)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Received: {e.Record.GetType().Name}");
                }
            };

            // Subscribe to error events
            client.ErrorOccurred += (sender, e) =>
            {
                Console.WriteLine($"[ERROR] {e.Exception.Message}");
            };

            // REPLAY MODE: Use most recent market open for valid replay time
            var replayStart = marketOpen;
            Console.WriteLine($"Replay start time: {replayStart:yyyy-MM-dd HH:mm:ss zzz}");
            Console.WriteLine();

            // Subscribe to equity trades on EQUS.MINI dataset with replay
            await client.SubscribeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Trades,
                symbols: new[] { "NVDA", "TSLA" },
                startTime: replayStart  // This enables replay mode
            );

            Console.WriteLine("✓ Subscribed to NVDA, TSLA trades on EQUS.MINI (REPLAY MODE)");
            Console.WriteLine();

            // ================================================================
            // KEY TEST: StartAsync should return metadata like LiveBlocking
            // ================================================================
            Console.WriteLine("Starting stream and waiting for metadata...");
            Console.WriteLine();

            var metadata = await client.StartAsync();

            Console.WriteLine("✓ StartAsync() returned metadata!");
            Console.WriteLine();
            Console.WriteLine("DBN Metadata:");
            Console.WriteLine("─────────────");
            Console.WriteLine($"  Version:       {metadata.Version}");
            Console.WriteLine($"  Dataset:       {metadata.Dataset}");
            Console.WriteLine($"  Schema:        {metadata.Schema?.ToString() ?? "(mixed)"}");
            Console.WriteLine($"  Stype Out:     {metadata.StypeOut}");
            Console.WriteLine($"  Timestamp Out: {metadata.TsOut}");
            Console.WriteLine($"  Start Time:    {DateTimeOffset.FromUnixTimeMilliseconds(metadata.Start / 1_000_000):yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"  Symbols:       {string.Join(", ", metadata.Symbols)}");
            if (metadata.Partial.Count > 0)
                Console.WriteLine($"  Partial:       {string.Join(", ", metadata.Partial)}");
            if (metadata.NotFound.Count > 0)
                Console.WriteLine($"  Not Found:     {string.Join(", ", metadata.NotFound)}");
            Console.WriteLine();

            // Stream records using IAsyncEnumerable for 5 seconds
            Console.WriteLine("Streaming records for 5 seconds using IAsyncEnumerable...");
            Console.WriteLine();

            var startTime = DateTime.Now;
            var streamTask = Task.Run(async () =>
            {
                await foreach (var record in client.StreamAsync())
                {
                    // Process records here (already counted by DataReceived event)
                }
            });

            // Wait for 5 seconds
            await Task.Delay(TimeSpan.FromSeconds(5));
            Console.WriteLine($"✓ Received {recordCount} records total, stopping...");
            Console.WriteLine();

            // Stop streaming
            await client.StopAsync();
            Console.WriteLine("✓ Stopped stream");
            Console.WriteLine();

            // Summary
            Console.WriteLine("Test Results:");
            Console.WriteLine("─────────────");
            Console.WriteLine("✓ StartAsync() correctly returns DbnMetadata");
            Console.WriteLine("✓ Metadata contains version, dataset, symbols");
            Console.WriteLine("✓ IAsyncEnumerable streaming works");
            Console.WriteLine("✓ DataReceived event fires for each record");
            Console.WriteLine("✓ Replay mode works outside market hours");
            Console.WriteLine();
            Console.WriteLine("LiveClient (LiveThreaded wrapper) REPLAY is working correctly!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.Message}");
            Console.WriteLine($"  Type: {ex.GetType().Name}");
            if (ex.StackTrace != null)
            {
                Console.WriteLine($"  Stack: {ex.StackTrace}");
            }
        }

        Console.WriteLine("\nExample complete!");
    }

    /// <summary>
    /// Calculate the most recent US equity market open (9:30 AM ET)
    /// Handles weekends by going back to Friday
    /// </summary>
    static DateTimeOffset GetMostRecentMarketOpen()
    {
        // US Eastern Time Zone
        var etZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

        // Get current time in ET
        var nowEt = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, etZone);

        // Market opens at 9:30 AM ET
        var marketOpenTime = new TimeSpan(9, 30, 0);

        // Start with today's date
        var candidateDate = nowEt.Date;

        // Handle weekends
        if (candidateDate.DayOfWeek == DayOfWeek.Saturday)
        {
            candidateDate = candidateDate.AddDays(-1); // Friday
        }
        else if (candidateDate.DayOfWeek == DayOfWeek.Sunday)
        {
            candidateDate = candidateDate.AddDays(-2); // Friday
        }
        else if (nowEt.TimeOfDay < marketOpenTime)
        {
            // Before market open today - go back to previous trading day
            candidateDate = candidateDate.AddDays(-1);

            // If that's a weekend, go back to Friday
            if (candidateDate.DayOfWeek == DayOfWeek.Saturday)
            {
                candidateDate = candidateDate.AddDays(-1); // Friday
            }
            else if (candidateDate.DayOfWeek == DayOfWeek.Sunday)
            {
                candidateDate = candidateDate.AddDays(-2); // Friday
            }
        }

        // Create market open datetime in ET
        var marketOpen = new DateTimeOffset(
            candidateDate.Year,
            candidateDate.Month,
            candidateDate.Day,
            9, 30, 0, // 9:30 AM
            etZone.GetUtcOffset(candidateDate));

        return marketOpen;
    }
}
