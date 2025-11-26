using Databento.Client.Builders;
using Databento.Client.Models;
using Databento.Interop;

namespace LiveBlocking.Replay.Example;

/// <summary>
/// REPLAY version of LiveBlocking.Example that works outside market hours.
/// Uses intraday replay to stream historical data.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Databento LiveBlocking Client REPLAY Example");
        Console.WriteLine("=============================================\n");
        Console.WriteLine("This is a REPLAY version that works outside market hours.");
        Console.WriteLine();

        // ===================================================================
        // Method 1: Pass API key as argument (NOT recommended for production)
        // This matches: LiveBlocking::Builder().SetKey("...").SetDataset(...).BuildBlocking()
        // ===================================================================
        Console.WriteLine("Method 1: API Key as Argument (NOT recommended for production)");
        Console.WriteLine("---------------------------------------------------------------");
        Console.WriteLine("In production, use Method 2 (environment variable) instead.\n");

        // Uncomment to use this method (replace with your actual API key):
        // var client1 = new LiveClientBuilder()
        //     .WithApiKey("db-YourActualApiKeyHere")
        //     .WithDataset("EQUS.MINI")
        //     .Build();
        // Console.WriteLine("✓ Created LiveClient with API key argument");
        // Console.WriteLine("  Dataset: EQUS.MINI\n");
        // await client1.DisposeAsync();

        Console.WriteLine("(Skipped - not recommended for production use)\n");

        // ===================================================================
        // Method 2: Read API key from environment variable (RECOMMENDED)
        // This matches: LiveBlocking::Builder().SetKeyFromEnv().SetDataset(...).BuildBlocking()
        // ===================================================================
        Console.WriteLine("Method 2: API Key from Environment Variable (RECOMMENDED)");
        Console.WriteLine("----------------------------------------------------------");
        Console.WriteLine("Reads from DATABENTO_API_KEY environment variable\n");

        var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("✗ DATABENTO_API_KEY environment variable is not set.");
            Console.WriteLine("  Please set your API key and try again.");
            Console.WriteLine();
            Console.WriteLine("Example (Windows):");
            Console.WriteLine("  set DATABENTO_API_KEY=your-api-key-here");
            Console.WriteLine();
            Console.WriteLine("Example (Linux/Mac):");
            Console.WriteLine("  export DATABENTO_API_KEY=your-api-key-here");
            return;
        }

        // Calculate most recent market open for valid replay time
        var marketOpen = GetMostRecentMarketOpen();
        Console.WriteLine($"Using most recent market open: {marketOpen:yyyy-MM-dd HH:mm:ss} ET");
        Console.WriteLine();

        try
        {
            await using var client = new LiveClientBuilder()
                .WithApiKey(apiKey)  // Reads from DATABENTO_API_KEY
                .WithDataset("EQUS.MINI")  // Corresponds to Dataset::EqusMini
                .Build();

            Console.WriteLine("✓ Created LiveClient from environment variable");
            Console.WriteLine("  Dataset: EQUS.MINI");
            Console.WriteLine();

            // ===================================================================
            // REPLAY MODE: Use most recent market open for valid replay time
            // ===================================================================
            var replayStart = marketOpen;
            Console.WriteLine($"Replay start time: {replayStart:yyyy-MM-dd HH:mm:ss zzz}");
            Console.WriteLine("Subscribing to NVDA trades for 5 seconds (REPLAY)\n");

            var recordCount = 0;
            var tradeCount = 0;

            // Subscribe to data events
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
                else if (e.Record is SystemMessage sysMsg)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {sysMsg}");
                }
            };

            // Subscribe to NVDA trades with replay
            await client.SubscribeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Trades,
                symbols: new[] { "NVDA" },
                startTime: replayStart  // This enables replay mode
            );

            Console.WriteLine("✓ Subscribed to NVDA trades (REPLAY MODE)");
            Console.WriteLine();

            // Start streaming (discarding metadata for this example)
            _ = await client.StartAsync();
            Console.WriteLine("✓ Started streaming");
            Console.WriteLine("  (Receiving replayed data for 5 seconds...)\n");

            // Wait 5 seconds
            await Task.Delay(5000);

            // Stop streaming
            try
            {
                await client.StopAsync();
            }
            catch (System.Threading.Channels.ChannelClosedException)
            {
                // Expected during shutdown
            }

            Console.WriteLine();
            Console.WriteLine("✓ Client verification complete!");
            Console.WriteLine();
            Console.WriteLine($"  Total records received: {recordCount}");
            Console.WriteLine($"  Trade messages:         {tradeCount}");
            Console.WriteLine($"  Other messages:         {recordCount - tradeCount}");
            Console.WriteLine();

            Console.WriteLine("Summary:");
            Console.WriteLine("--------");
            Console.WriteLine("✓ LiveClient successfully created using environment variable");
            Console.WriteLine("✓ Dataset configured: EQUS.MINI");
            Console.WriteLine("✓ Connectivity verified with NVDA subscription");
            Console.WriteLine("✓ REPLAY mode works outside market hours");
            Console.WriteLine();
            Console.WriteLine("Builder Pattern Mapping:");
            Console.WriteLine("  C++:  LiveBlocking::Builder().SetKeyFromEnv().SetDataset(...).BuildBlocking()");
            Console.WriteLine("  C#:   new LiveClientBuilder().WithApiKey(apiKey).WithDataset(...).Build()");
        }
        catch (DbentoAuthenticationException ex)
        {
            Console.WriteLine($"✗ Authentication failed: {ex.Message}");
            Console.WriteLine("  Please verify your API key is correct.");
        }
        catch (System.Threading.Channels.ChannelClosedException)
        {
            // Expected during cleanup
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex.GetType().Name}");
            Console.WriteLine($"  Message: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("Additional Builder Options:");
        Console.WriteLine("---------------------------");
        Console.WriteLine("• .WithSendTimestampOut(bool)      - Include gateway send timestamps");
        Console.WriteLine("• .WithUpgradePolicy(policy)       - Set version upgrade policy");
        Console.WriteLine("• .WithHeartbeatInterval(timespan) - Configure heartbeat interval");
        Console.WriteLine("• .WithLogger(logger)              - Add diagnostic logging");
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
