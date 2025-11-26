using Databento.Client.Builders;
using Databento.Client.Models;
using Databento.Interop;

namespace LiveAuthentication.Replay.Example;

/// <summary>
/// REPLAY version of LiveAuthentication.Example that works outside market hours.
/// Uses intraday replay to stream historical data.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Databento Live Client Authentication REPLAY Example");
        Console.WriteLine("====================================================\n");
        Console.WriteLine("This is a REPLAY version that works outside market hours.");
        Console.WriteLine();

        // Read API key from environment variable (matching SetKeyFromEnv())
        var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("ERROR: DATABENTO_API_KEY environment variable is not set.");
            Console.WriteLine();
            Console.WriteLine("Please set your API key:");
            Console.WriteLine("  Windows (PowerShell): $env:DATABENTO_API_KEY=\"your-api-key\"");
            Console.WriteLine("  Linux/macOS:          export DATABENTO_API_KEY=\"your-api-key\"");
            Console.WriteLine();
            Console.WriteLine("You can get your API key from: https://databento.com/portal/keys");
            return;
        }

        Console.WriteLine("✓ API key found in environment variable");
        Console.WriteLine($"  Key: {apiKey.Substring(0, 8)}... (masked)");
        Console.WriteLine();

        // Calculate most recent market open for valid replay time
        var marketOpen = GetMostRecentMarketOpen();
        Console.WriteLine($"Using most recent market open: {marketOpen:yyyy-MM-dd HH:mm:ss} ET");
        Console.WriteLine();

        try
        {
            await using var client = new LiveClientBuilder()
                .WithApiKey(apiKey)
                .WithDataset("EQUS.MINI")  // Default dataset
                .Build();

            Console.WriteLine("✓ Successfully created LiveClient");
            Console.WriteLine("  Default dataset: EQUS.MINI");
            Console.WriteLine();

            // REPLAY MODE: Use most recent market open for valid replay time
            var replayStart = marketOpen;
            Console.WriteLine($"Replay start time: {replayStart:yyyy-MM-dd HH:mm:ss zzz}");
            Console.WriteLine("Subscribing to NVDA trades on EQUS.MINI dataset (REPLAY)\n");

            // Subscribe to data events
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
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Trade #{tradeCount}: " +
                            $"InstrumentId={trade.InstrumentId} " +
                            $"Price=${trade.PriceDecimal:F2} " +
                            $"Size={trade.Size} " +
                            $"Side={trade.Side}");
                    }
                }
                else if (recordCount <= 3)
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Record #{recordCount}: {e.Record.GetType().Name}");
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

            // Start streaming
            Console.WriteLine("Starting live stream with replay...");
            Console.WriteLine("(Receiving first 5 records to verify authentication)");
            Console.WriteLine();

            _ = await client.StartAsync();

            // Wait for a few records to confirm authentication works
            var timeout = DateTime.Now.AddSeconds(30);
            while (recordCount < 5 && DateTime.Now < timeout)
            {
                await Task.Delay(100);
            }

            // Stop streaming
            try
            {
                await client.StopAsync();
            }
            catch (System.Threading.Channels.ChannelClosedException)
            {
                // Expected during shutdown - ignore
            }

            Console.WriteLine();
            if (recordCount > 0)
            {
                Console.WriteLine($"✓ Authentication successful! Received {recordCount} records.");
                Console.WriteLine("✓ REPLAY mode verified working outside market hours.");
            }
            else
            {
                Console.WriteLine("⚠ No records received within timeout period.");
                Console.WriteLine("  Check the replay date/time range has available data.");
            }
        }
        catch (DbentoAuthenticationException ex)
        {
            Console.WriteLine($"✗ Authentication failed: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Please check:");
            Console.WriteLine("  1. Your API key is correct");
            Console.WriteLine("  2. Your API key has live streaming permissions");
            Console.WriteLine("  3. You can view/manage keys at: https://databento.com/portal/keys");
        }
        catch (System.Threading.Channels.ChannelClosedException)
        {
            // Expected during cleanup - ignore
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Unexpected error: {ex.GetType().Name}");
            Console.WriteLine($"  Message: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("Authentication REPLAY example complete.");
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
