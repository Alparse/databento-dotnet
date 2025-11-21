using Databento.Client.Builders;
using Databento.Client.Models;
using Databento.Interop;
using System.Diagnostics;

Console.WriteLine("=== Live API Invalid Symbol Test ===");
Console.WriteLine();

var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.WriteLine("ERROR: DATABENTO_API_KEY environment variable not set");
    return 1;
}

Console.WriteLine($"API Key: {apiKey[..10]}... (masked)");
Console.WriteLine();

// Test 1: Normal Live Mode with Invalid Symbol
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("TEST 1: Live Normal Mode - Invalid Symbol");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("Expected: Graceful handling via metadata.not_found");
Console.WriteLine();

try
{
    var client1 = new LiveClientBuilder()
        .WithApiKey(apiKey)
        .WithDataset("EQUS.MINI")
        .Build();

    Console.WriteLine("✓ Created Live client");
    Console.WriteLine();

    // Subscribe with mix of valid and invalid symbols
    Console.WriteLine("Subscribing to invalid 'BADTICKER' and valid 'NVDA'...");
    await client1.SubscribeAsync(
        dataset: "EQUS.MINI",
        schema: Schema.Trades,
        symbols: ["BADTICKER", "NVDA"]);  // "BADTICKER" invalid, "NVDA" valid

    Console.WriteLine("✓ Subscribe succeeded (no error yet)");
    Console.WriteLine();

    Console.WriteLine("Starting stream...");
    var stopwatch = Stopwatch.StartNew();
    var metadata = await client1.StartAsync();
    stopwatch.Stop();

    Console.WriteLine($"✓ StartAsync completed in {stopwatch.ElapsedMilliseconds}ms");
    Console.WriteLine();

    Console.WriteLine("Metadata Results:");
    Console.WriteLine($"  Dataset: {metadata.Dataset}");
    Console.WriteLine($"  Schema: {metadata.Schema}");
    Console.WriteLine($"  Valid symbols: [{string.Join(", ", metadata.Symbols)}]");
    Console.WriteLine($"  Not found: [{string.Join(", ", metadata.NotFound)}]");
    Console.WriteLine($"  Partial: [{string.Join(", ", metadata.Partial)}]");
    Console.WriteLine();

    if (metadata.NotFound.Contains("BADTICKER"))
    {
        Console.WriteLine("✅ TEST 1 PASSED: Invalid symbol 'BADTICKER' correctly in not_found");
    }
    else
    {
        Console.WriteLine("❌ TEST 1 FAILED: 'BADTICKER' not in not_found");
    }

    if (metadata.Symbols.Contains("NVDA"))
    {
        Console.WriteLine("✅ TEST 1 PASSED: Valid symbol 'NVDA' in symbols list");
    }
    else
    {
        Console.WriteLine("❌ TEST 1 FAILED: 'NVDA' not in symbols");
    }

    Console.WriteLine();
    Console.WriteLine("Receiving data for 3 seconds to verify only CLZ5 data comes through...");

    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    int recordCount = 0;
    HashSet<uint> instrumentIds = new HashSet<uint>();

    try
    {
        await foreach (var record in client1.StreamAsync(cts.Token))
        {
            recordCount++;
            instrumentIds.Add(record.InstrumentId);

            if (recordCount <= 3)
            {
                Console.WriteLine($"  Record {recordCount}: {record.RType} InstrumentId={record.InstrumentId}");
            }
        }
    }
    catch (OperationCanceledException)
    {
        // Expected timeout
    }

    Console.WriteLine($"✓ Received {recordCount} records for {instrumentIds.Count} instrument(s)");
    Console.WriteLine();

    await client1.DisposeAsync();
    Console.WriteLine("✓ Client disposed");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ TEST 1 FAILED with exception:");
    Console.WriteLine($"   Type: {ex.GetType().Name}");
    Console.WriteLine($"   Message: {ex.Message}");

    if (ex.Message.Contains("Native library crashed"))
    {
        Console.WriteLine();
        Console.WriteLine("💥 CRITICAL: Live normal mode CRASHES with invalid symbols!");
    }
}

Console.WriteLine();
Console.WriteLine();

// Test 2: Live Replay Mode with Invalid Symbol
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("TEST 2: Live Replay Mode - Invalid Symbol");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("Expected: Unknown (testing to find out)");
Console.WriteLine("Risk: May crash like Historical API");
Console.WriteLine();

try
{
    var client2 = new LiveClientBuilder()
        .WithApiKey(apiKey)
        .WithDataset("EQUS.MINI")
        .Build();

    Console.WriteLine("✓ Created Live client");
    Console.WriteLine();

    // Subscribe with REPLAY and invalid symbol
    var replayStart = new DateTimeOffset(DateTime.Parse("11/17/2025 09:30:00"), TimeSpan.FromHours(-5));

    Console.WriteLine($"Subscribing to invalid 'BADTICKER' with REPLAY from {replayStart}...");
    await client2.SubscribeAsync(
        dataset: "EQUS.MINI",
        schema: Schema.Trades,
        symbols: ["BADTICKER"],  // Invalid symbol
        startTime: replayStart);

    Console.WriteLine("✓ Subscribe succeeded (no error yet)");
    Console.WriteLine();

    Console.WriteLine("Starting replay stream...");
    var stopwatch = Stopwatch.StartNew();
    var metadata = await client2.StartAsync();
    stopwatch.Stop();

    Console.WriteLine($"✓ StartAsync completed in {stopwatch.ElapsedMilliseconds}ms");
    Console.WriteLine();

    Console.WriteLine("Metadata Results:");
    Console.WriteLine($"  Dataset: {metadata.Dataset}");
    Console.WriteLine($"  Schema: {metadata.Schema}");
    Console.WriteLine($"  Valid symbols: [{string.Join(", ", metadata.Symbols)}]");
    Console.WriteLine($"  Not found: [{string.Join(", ", metadata.NotFound)}]");
    Console.WriteLine($"  Partial: [{string.Join(", ", metadata.Partial)}]");
    Console.WriteLine();

    if (metadata.NotFound.Contains("BADTICKER"))
    {
        Console.WriteLine("✅ TEST 2 PASSED: Invalid symbol 'BADTICKER' gracefully in not_found");
        Console.WriteLine("✅ GOOD NEWS: Replay mode does NOT crash with invalid symbols!");
    }
    else
    {
        Console.WriteLine("⚠️  'BADTICKER' not in not_found - unexpected behavior");
    }

    Console.WriteLine();
    await client2.DisposeAsync();
    Console.WriteLine("✓ Client disposed");
}
catch (DbentoException ex)
{
    Console.WriteLine($"❌ TEST 2 FAILED with DbentoException:");
    Console.WriteLine($"   Message: {ex.Message}");
    Console.WriteLine($"   ErrorCode: {ex.ErrorCode}");

    if (ex.Message.Contains("Native library crashed"))
    {
        Console.WriteLine();
        Console.WriteLine("💥 CRITICAL: Live replay mode CRASHES with invalid symbols!");
        Console.WriteLine("This confirms the bug extends to Live Replay mode.");
        Console.WriteLine("Mitigation is REQUIRED for Live API.");
    }
    else
    {
        Console.WriteLine();
        Console.WriteLine("✅ GOOD: Proper exception thrown (not a crash)");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ TEST 2 FAILED with unexpected exception:");
    Console.WriteLine($"   Type: {ex.GetType().Name}");
    Console.WriteLine($"   Message: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine();

// Test 3: Live Replay Mode with Valid Symbol (baseline)
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("TEST 3: Live Replay Mode - Valid Symbol (Baseline)");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("Expected: Should work normally");
Console.WriteLine();

try
{
    var client3 = new LiveClientBuilder()
        .WithApiKey(apiKey)
        .WithDataset("EQUS.MINI")
        .Build();

    Console.WriteLine("✓ Created Live client");
    Console.WriteLine();

    var replayStart = new DateTimeOffset(DateTime.Parse("11/17/2025 09:30:00"), TimeSpan.FromHours(-5));

    Console.WriteLine($"Subscribing to valid 'NVDA' with REPLAY from {replayStart}...");
    await client3.SubscribeAsync(
        dataset: "EQUS.MINI",
        schema: Schema.Trades,
        symbols: ["NVDA"],  // Valid symbol
        startTime: replayStart);

    Console.WriteLine("✓ Subscribe succeeded");
    Console.WriteLine();

    Console.WriteLine("Starting replay stream...");
    var stopwatch = Stopwatch.StartNew();
    var metadata = await client3.StartAsync();
    stopwatch.Stop();

    Console.WriteLine($"✓ StartAsync completed in {stopwatch.ElapsedMilliseconds}ms");
    Console.WriteLine();

    Console.WriteLine("Metadata Results:");
    Console.WriteLine($"  Dataset: {metadata.Dataset}");
    Console.WriteLine($"  Schema: {metadata.Schema}");
    Console.WriteLine($"  Valid symbols: [{string.Join(", ", metadata.Symbols)}]");
    Console.WriteLine($"  Not found: [{string.Join(", ", metadata.NotFound)}]");
    Console.WriteLine();

    Console.WriteLine("Receiving first 5 records...");
    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    int recordCount = 0;

    try
    {
        await foreach (var record in client3.StreamAsync(cts.Token))
        {
            recordCount++;
            if (recordCount <= 5)
            {
                Console.WriteLine($"  Record {recordCount}: {record.RType} InstrumentId={record.InstrumentId}");
            }

            if (recordCount >= 5)
                break;
        }
    }
    catch (OperationCanceledException)
    {
        // Expected
    }

    Console.WriteLine($"✓ Received {recordCount} records");

    if (recordCount > 0)
    {
        Console.WriteLine("✅ TEST 3 PASSED: Replay mode works with valid symbols");
    }
    else
    {
        Console.WriteLine("⚠️  No records received (may be no data at that time)");
    }

    Console.WriteLine();
    await client3.DisposeAsync();
    Console.WriteLine("✓ Client disposed");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ TEST 3 FAILED:");
    Console.WriteLine($"   Type: {ex.GetType().Name}");
    Console.WriteLine($"   Message: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine();

// Test 4: Live Normal Mode with Invalid Dataset
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("TEST 4: Live Normal Mode - Invalid Dataset");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("Expected: Unknown");
Console.WriteLine();

try
{
    var client4 = new LiveClientBuilder()
        .WithApiKey(apiKey)
        .WithDataset("INVALID.DATASET")
        .Build();

    Console.WriteLine("✓ Created Live client with invalid dataset");
    Console.WriteLine();

    Console.WriteLine("Subscribing to valid symbol but invalid dataset...");
    await client4.SubscribeAsync(
        dataset: "INVALID.DATASET",
        schema: Schema.Trades,
        symbols: ["NVDA"]);

    Console.WriteLine("✓ Subscribe succeeded (no error yet)");
    Console.WriteLine();

    Console.WriteLine("Starting stream...");
    var metadata = await client4.StartAsync();

    Console.WriteLine($"✓ StartAsync completed");
    Console.WriteLine();
    Console.WriteLine("⚠️  Unexpected: Invalid dataset did not cause error");

    await client4.DisposeAsync();
}
catch (DbentoException ex)
{
    Console.WriteLine($"✓ Caught DbentoException (expected):");
    Console.WriteLine($"   Message: {ex.Message}");
    Console.WriteLine($"   ErrorCode: {ex.ErrorCode}");

    if (ex.Message.Contains("Native library crashed"))
    {
        Console.WriteLine("💥 CRITICAL: Invalid dataset causes native crash!");
    }
    else
    {
        Console.WriteLine("✅ GOOD: Proper exception without crash");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Caught exception:");
    Console.WriteLine($"   Type: {ex.GetType().Name}");
    Console.WriteLine($"   Message: {ex.Message}");
}

Console.WriteLine();
Console.WriteLine();

// Summary
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine("TEST SUMMARY");
Console.WriteLine("═══════════════════════════════════════════════════════════");
Console.WriteLine();
Console.WriteLine("Test Results:");
Console.WriteLine("  1. Live Normal + Invalid Symbol: See above");
Console.WriteLine("  2. Live Replay + Invalid Symbol: See above");
Console.WriteLine("  3. Live Replay + Valid Symbol: See above");
Console.WriteLine("  4. Live Normal + Invalid Dataset: See above");
Console.WriteLine();
Console.WriteLine("Key Findings:");
Console.WriteLine("  - If any test shows 'Native library crashed': MITIGATION REQUIRED");
Console.WriteLine("  - If all tests graceful: Live API is safer than Historical API");
Console.WriteLine();

return 0;
