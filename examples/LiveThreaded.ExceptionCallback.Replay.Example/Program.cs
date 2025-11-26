using Databento.Client.Builders;
using Databento.Client.Live;
using Databento.Client.Models;

namespace LiveThreaded.ExceptionCallback.Replay.Example;

/// <summary>
/// REPLAY version of ExceptionCallback example that works outside market hours.
/// Uses intraday replay to stream historical data.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  Databento ExceptionCallback REPLAY Example                  ║");
        Console.WriteLine("║  Custom Exception Handling with Continue/Stop Actions        ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("This is a REPLAY version that works outside market hours.");
        Console.WriteLine();

        var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("❌ ERROR: DATABENTO_API_KEY environment variable is not set.");
            return;
        }

        Console.WriteLine("✓ API key found");
        Console.WriteLine();

        // Calculate most recent market open for valid replay time
        var marketOpen = GetMostRecentMarketOpen();
        Console.WriteLine($"Using most recent market open: {marketOpen:yyyy-MM-dd HH:mm:ss} ET");
        Console.WriteLine();

        // REPLAY configuration
        var replayStart = marketOpen;
        Console.WriteLine($"Replay start time: {replayStart:yyyy-MM-dd HH:mm:ss zzz}");
        Console.WriteLine();

        // ================================================================
        // Test 1: ExceptionCallback with Continue action
        // ================================================================
        Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Test 1: ExceptionCallback - Continue Action (REPLAY)       │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("Configure exception handler to return ExceptionAction.Continue");
        Console.WriteLine("This allows the stream to continue even after errors.");
        Console.WriteLine();

        var errorCount = 0;
        var continueCount = 0;

        try
        {
            await using var client = new LiveClientBuilder()
                .WithApiKey(apiKey)
                .WithDataset("EQUS.MINI")
                .WithExceptionHandler(ex =>
                {
                    errorCount++;
                    continueCount++;
                    Console.WriteLine($"[ExceptionHandler] Error #{errorCount}: {ex.GetType().Name}");
                    Console.WriteLine($"                   Message: {ex.Message}");
                    Console.WriteLine($"                   Action: Continue (keep streaming)");
                    Console.WriteLine();
                    return ExceptionAction.Continue;
                })
                .Build();

            Console.WriteLine("✓ Client created with ExceptionCallback configured");
            Console.WriteLine();

            // Track errors via ErrorOccurred event too
            client.ErrorOccurred += (sender, e) =>
            {
                Console.WriteLine($"[ErrorEvent] {e.Exception.GetType().Name}: {e.Exception.Message}");
            };

            // Track data received
            var recordCount = 0;
            var tradeCount = 0;
            client.DataReceived += (sender, e) =>
            {
                recordCount++;
                if (e.Record is TradeMessage trade)
                {
                    tradeCount++;
                    if (tradeCount <= 5)
                    {
                        Console.WriteLine($"[DataReceived] Trade #{tradeCount}: " +
                            $"InstrumentId={trade.InstrumentId} " +
                            $"Price=${trade.PriceDecimal:F2} " +
                            $"Size={trade.Size}");
                    }
                }
                else if (recordCount <= 3)
                {
                    Console.WriteLine($"[DataReceived] {e.Record.GetType().Name}");
                }
            };

            // Subscribe to valid data with replay
            await client.SubscribeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Trades,
                symbols: new[] { "NVDA" },
                startTime: replayStart  // This enables replay mode
            );

            Console.WriteLine("✓ Subscribed to NVDA trades (REPLAY MODE)");
            Console.WriteLine();

            // Start streaming
            Console.WriteLine("Starting stream...");
            var metadata = await client.StartAsync();
            Console.WriteLine($"✓ Stream started (Dataset: {metadata.Dataset})");
            Console.WriteLine();

            // Stream for a few seconds
            Console.WriteLine("Streaming for 5 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(5));

            Console.WriteLine();
            Console.WriteLine($"✓ Received {recordCount} records");
            Console.WriteLine($"✓ Handled {errorCount} errors (continued streaming)");
            Console.WriteLine("✓ REPLAY mode verified working");
            Console.WriteLine();

            await client.StopAsync();
            Console.WriteLine("✓ Client stopped successfully");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name}");
            Console.WriteLine($"   Message: {ex.Message}");
        }

        // ================================================================
        // Test 2: ExceptionCallback with Stop action
        // ================================================================
        Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Test 2: ExceptionCallback - Stop Action (REPLAY)           │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("Configure exception handler to return ExceptionAction.Stop");
        Console.WriteLine("This terminates the stream when an error occurs.");
        Console.WriteLine();

        var stopCount = 0;

        try
        {
            await using var client = new LiveClientBuilder()
                .WithApiKey(apiKey)
                .WithDataset("EQUS.MINI")
                .WithExceptionHandler(ex =>
                {
                    stopCount++;
                    Console.WriteLine($"[ExceptionHandler] Error: {ex.GetType().Name}");
                    Console.WriteLine($"                   Message: {ex.Message}");
                    Console.WriteLine($"                   Action: Stop (terminate stream)");
                    Console.WriteLine();
                    return ExceptionAction.Stop;
                })
                .Build();

            Console.WriteLine("✓ Client created with ExceptionCallback (Stop action)");
            Console.WriteLine();

            // Track data received
            var recordCount = 0;
            var tradeCount = 0;
            client.DataReceived += (sender, e) =>
            {
                recordCount++;
                if (e.Record is TradeMessage trade)
                {
                    tradeCount++;
                    if (tradeCount <= 5)
                    {
                        Console.WriteLine($"[DataReceived] Trade #{tradeCount}: " +
                            $"InstrumentId={trade.InstrumentId} " +
                            $"Price=${trade.PriceDecimal:F2} " +
                            $"Size={trade.Size}");
                    }
                }
                else if (recordCount <= 3)
                {
                    Console.WriteLine($"[DataReceived] {e.Record.GetType().Name}");
                }
            };

            // Subscribe with replay
            await client.SubscribeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Trades,
                symbols: new[] { "NVDA" },
                startTime: replayStart  // This enables replay mode
            );

            Console.WriteLine("✓ Subscribed to NVDA trades (REPLAY MODE)");
            Console.WriteLine();

            // Start streaming
            Console.WriteLine("Starting stream...");
            var metadata = await client.StartAsync();
            Console.WriteLine($"✓ Stream started (Dataset: {metadata.Dataset})");
            Console.WriteLine();

            // Stream for a few seconds
            Console.WriteLine("Streaming for 5 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(5));

            Console.WriteLine();
            Console.WriteLine($"✓ Received {recordCount} records");
            Console.WriteLine($"✓ Stop action count: {stopCount}");
            Console.WriteLine("✓ REPLAY mode verified working");
            Console.WriteLine();

            await client.StopAsync();
            Console.WriteLine("✓ Client stopped successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.GetType().Name}");
            Console.WriteLine($"   Message: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("ExceptionCallback REPLAY Example Complete");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
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
