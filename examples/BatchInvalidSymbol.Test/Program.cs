using Databento.Client.Builders;
using Databento.Client.Models;
using Databento.Interop;

Console.WriteLine("=== Batch API Invalid Symbol Test ===");
Console.WriteLine();

var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("ERROR: DATABENTO_API_KEY environment variable not set");
    return 1;
}

Console.WriteLine($"API Key: {apiKey[..10]}... (masked)");
Console.WriteLine();

// Test: Batch API with Invalid Symbol
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("TEST: Batch API - Invalid Symbol");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("Expected: Unknown (testing to find out)");
Console.WriteLine("Risk: May crash like Historical API (both use HTTP)");
Console.WriteLine();

try
{
    var client = new HistoricalClientBuilder()
        .WithApiKey(apiKey)
        .Build();

    Console.WriteLine("✓ Created Historical client");
    Console.WriteLine();

    // Submit batch job with invalid symbol "CL" (should be "CLZ5")
    Console.WriteLine("Submitting batch job with invalid symbol 'CL'...");
    Console.WriteLine("  Dataset: GLBX.MDP3");
    Console.WriteLine("  Symbols: [\"CL\"]  // Invalid - should be CLZ5 or similar");
    Console.WriteLine("  Schema: Trades");
    Console.WriteLine("  Date Range: 2023-11-14 to 2023-11-15");
    Console.WriteLine();

    var startTime = new DateTimeOffset(2023, 11, 14, 0, 0, 0, TimeSpan.Zero);
    var endTime = new DateTimeOffset(2023, 11, 15, 0, 0, 0, TimeSpan.Zero);

    var job = await client.BatchSubmitJobAsync(
        dataset: "GLBX.MDP3",
        symbols: new[] { "CL" },  // Invalid symbol
        schema: Schema.Trades,
        startTime: startTime,
        endTime: endTime);

    Console.WriteLine("✓ BatchSubmitJobAsync completed");
    Console.WriteLine();
    Console.WriteLine("Job Details:");
    Console.WriteLine($"  Job ID: {job.Id}");
    Console.WriteLine($"  State: {job.State}");
    Console.WriteLine($"  Cost: ${job.CostUsd:F2}");
    Console.WriteLine($"  Symbols: {string.Join(", ", job.Symbols)}");
    Console.WriteLine();

    Console.WriteLine("✅ TEST PASSED: Batch API handles invalid symbols gracefully!");
    Console.WriteLine("✅ GOOD NEWS: Batch API does NOT crash with invalid symbols!");
}
catch (DbentoException ex)
{
    Console.WriteLine($"✓ Caught DbentoException (expected):");
    Console.WriteLine($"   Message: {ex.Message}");
    Console.WriteLine($"   ErrorCode: {ex.ErrorCode}");

    if (ex.Message.Contains("Native library crashed"))
    {
        Console.WriteLine();
        Console.WriteLine("💥 CRITICAL: Batch API CRASHES with invalid symbols!");
        Console.WriteLine("This confirms the bug extends to Batch API.");
    }
    else
    {
        Console.WriteLine();
        Console.WriteLine("✅ GOOD: Proper exception thrown (not a crash)");
        Console.WriteLine("✅ TEST PASSED: Batch API handles errors gracefully");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ TEST FAILED with unexpected exception:");
    Console.WriteLine($"   Type: {ex.GetType().Name}");
    Console.WriteLine($"   Message: {ex.Message}");

    if (ex is AccessViolationException || ex is System.Runtime.InteropServices.SEHException)
    {
        Console.WriteLine();
        Console.WriteLine("💥 CRITICAL: Native crash detected!");
    }
}

Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("TEST COMPLETE");
Console.WriteLine("═══════════════════════════════════════════════════════════");

return 0;
