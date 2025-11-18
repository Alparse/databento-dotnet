using Databento.Client.Builders;
using Databento.Interop;

namespace Reference.Example;

/// <summary>
/// Simple example demonstrating the Reference API
/// Matches the Python example: client.security_master.get_last(symbols="AAPL")
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  Databento Reference API Example                             ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("❌ ERROR: DATABENTO_API_KEY environment variable is not set.");
            Console.WriteLine();
            Console.WriteLine("Set your API key:");
            Console.WriteLine("  Windows: $env:DATABENTO_API_KEY=\"your-api-key\"");
            Console.WriteLine("  Linux:   export DATABENTO_API_KEY=\"your-api-key\"");
            Console.WriteLine();
            Console.WriteLine("Get your key from: https://databento.com/portal/keys");
            return;
        }

        Console.WriteLine("✓ API key found");
        Console.WriteLine();

        try
        {
            // Create Reference client using builder
            await using var client = new ReferenceClientBuilder()
                .WithApiKey(apiKey)
                .Build();

            Console.WriteLine("✓ Reference client created successfully");
            Console.WriteLine();

            // Example 1: Get latest security master data for NVDA
            Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ Example 1: Security Master - Get Last                      │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("Python:  client.security_master.get_last(symbols=\"NVDA\")");
            Console.WriteLine("C#:      await client.SecurityMaster.GetLastAsync(new[] { \"NVDA\" })");
            Console.WriteLine();

            var records = await client.SecurityMaster.GetLastAsync(
                symbols: new[] { "NVDA" },
                countries: new[] { "US" }
            );

            Console.WriteLine($"Received {records.Count} record(s)");
            Console.WriteLine();

            if (records.Count > 0)
            {
                var record = records[0];
                Console.WriteLine("Security Master Record:");
                Console.WriteLine($"  Issuer:              {record.IssuerName}");
                Console.WriteLine($"  Security Type:       {record.SecurityType}");
                Console.WriteLine($"  Description:         {record.SecurityDescription}");
                Console.WriteLine($"  Symbol:              {record.Symbol}");
                Console.WriteLine($"  ISIN:                {record.Isin}");
                Console.WriteLine($"  Exchange:            {record.Exchange}");
                Console.WriteLine($"  Trading Currency:    {record.TradingCurrency}");
                Console.WriteLine($"  Shares Outstanding:  {record.SharesOutstanding:N0}");
                Console.WriteLine($"  Effective Date:      {record.TsEffective:yyyy-MM-dd}");
                Console.WriteLine($"  Listing Country:     {record.ListingCountry}");
            }
            Console.WriteLine();

            // Example 2: Get adjustment factors for NVDA
            Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ Example 2: Adjustment Factors                               │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("Python:  client.adjustment_factors.get_range(");
            Console.WriteLine("             symbols=\"NVDA\", start=\"2023\", end=\"2024\")");
            Console.WriteLine("C#:      await client.AdjustmentFactors.GetRangeAsync(");
            Console.WriteLine("             new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),");
            Console.WriteLine("             new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),");
            Console.WriteLine("             new[] { \"NVDA\" })");
            Console.WriteLine();

            var adjustments = await client.AdjustmentFactors.GetRangeAsync(
                start: new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero),
                end: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                symbols: new[] { "NVDA" },
                countries: new[] { "US" }
            );

            Console.WriteLine($"Received {adjustments.Count} adjustment factor record(s)");
            Console.WriteLine();

            if (adjustments.Count > 0)
            {
                Console.WriteLine("First 3 adjustment records:");
                foreach (var adj in adjustments.Take(3))
                {
                    Console.WriteLine($"  Ex-Date: {adj.ExDate:yyyy-MM-dd}, Event: {adj.Event}, " +
                                    $"Dividend: {adj.DividendCurrency} {adj.DividendAmount:F2}, " +
                                    $"Frequency: {adj.Frequency}");
                }
            }
            Console.WriteLine();

            // Example 3: Get corporate actions for NVDA (stock split in June 2024)
            Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│ Example 3: Corporate Actions                                │");
            Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("Python:  client.corporate_actions.get_range(");
            Console.WriteLine("             symbols=\"NVDA\", start=\"2024-01\")");
            Console.WriteLine("C#:      await client.CorporateActions.GetRangeAsync(");
            Console.WriteLine("             new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),");
            Console.WriteLine("             symbols: new[] { \"NVDA\" })");
            Console.WriteLine();

            var actions = await client.CorporateActions.GetRangeAsync(
                start: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
                symbols: new[] { "NVDA" },
                countries: new[] { "US" }
            );

            Console.WriteLine($"Received {actions.Count} corporate action record(s)");
            Console.WriteLine();

            if (actions.Count > 0)
            {
                Console.WriteLine($"Corporate actions for NVDA (showing first 5):");
                foreach (var action in actions.Take(5))
                {
                    Console.WriteLine($"  Event: {action.Event}, Date: {action.EventDate:yyyy-MM-dd}, " +
                                    $"Symbol: {action.Symbol}");
                    if (action.OldSharesOutstanding.HasValue && action.NewSharesOutstanding.HasValue)
                    {
                        Console.WriteLine($"    Shares: {action.OldSharesOutstanding:N0} → {action.NewSharesOutstanding:N0}");
                    }
                }
            }
            Console.WriteLine();

            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  Reference API Examples Complete                              ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        }
        catch (ValidationException ex)
        {
            Console.WriteLine($"❌ Client/Validation error: {ex.Message}");
        }
        catch (ServerException ex)
        {
            Console.WriteLine($"❌ Server error: {ex.Message}");
        }
        catch (DbentoException ex)
        {
            Console.WriteLine($"❌ Databento error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Unexpected error: {ex.GetType().Name}");
            Console.WriteLine($"   Message: {ex.Message}");
            if (ex.StackTrace != null)
            {
                Console.WriteLine($"   Stack: {ex.StackTrace}");
            }
        }
    }
}
