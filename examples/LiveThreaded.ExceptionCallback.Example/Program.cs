using Databento.Client.Builders;
using Databento.Client.Live;
using Databento.Client.Models;

namespace LiveThreaded.ExceptionCallback.Example;

/// <summary>
/// Demonstrates ExceptionCallback feature - custom exception handling with Continue/Stop actions
///
/// This example shows how to:
/// 1. Configure an exception handler at client creation
/// 2. Handle errors with ExceptionAction.Continue (keep streaming)
/// 3. Handle errors with ExceptionAction.Stop (terminate stream)
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  Databento ExceptionCallback Example                         ║");
        Console.WriteLine("║  Custom Exception Handling with Continue/Stop Actions        ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("❌ ERROR: DATABENTO_API_KEY environment variable is not set.");
            return;
        }

        Console.WriteLine("✓ API key found");
        Console.WriteLine();

        // ================================================================
        // Test 1: ExceptionCallback with Continue action
        // ================================================================
        Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Test 1: ExceptionCallback - Continue Action                │");
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
            client.DataReceived += (sender, e) =>
            {
                recordCount++;
                if (recordCount <= 3)
                {
                    Console.WriteLine($"[DataReceived] {e.Record.GetType().Name}");
                }
            };

            // Subscribe to valid data
            await client.SubscribeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Trades,
                symbols: new[] { "NVDA" }
            );

            Console.WriteLine("✓ Subscribed to NVDA trades");
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
        Console.WriteLine("│ Test 2: ExceptionCallback - Stop Action                    │");
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
            client.DataReceived += (sender, e) =>
            {
                recordCount++;
                if (recordCount <= 3)
                {
                    Console.WriteLine($"[DataReceived] {e.Record.GetType().Name}");
                }
            };

            // Subscribe to valid data
            await client.SubscribeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Trades,
                symbols: new[] { "TSLA" }
            );

            Console.WriteLine("✓ Subscribed to TSLA trades");
            Console.WriteLine();

            // Start streaming
            Console.WriteLine("Starting stream...");
            var metadata = await client.StartAsync();
            Console.WriteLine($"✓ Stream started (Dataset: {metadata.Dataset})");
            Console.WriteLine();

            // Stream for a few seconds
            Console.WriteLine("Streaming for 5 seconds (or until error stops it)...");
            await Task.Delay(TimeSpan.FromSeconds(5));

            Console.WriteLine();
            Console.WriteLine($"✓ Received {recordCount} records");

            if (stopCount > 0)
            {
                Console.WriteLine($"✓ Stream was stopped by exception handler after {stopCount} error(s)");
            }
            else
            {
                Console.WriteLine($"✓ No errors occurred during streaming");
            }
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
        // Test 3: Without ExceptionCallback (default behavior)
        // ================================================================
        Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│ Test 3: No ExceptionCallback (Default Behavior)            │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("Without exception handler, errors are only raised via ErrorOccurred event.");
        Console.WriteLine();

        try
        {
            await using var client = new LiveClientBuilder()
                .WithApiKey(apiKey)
                .WithDataset("EQUS.MINI")
                .Build();

            Console.WriteLine("✓ Client created without ExceptionCallback");
            Console.WriteLine();

            var eventErrorCount = 0;
            client.ErrorOccurred += (sender, e) =>
            {
                eventErrorCount++;
                Console.WriteLine($"[ErrorEvent #{eventErrorCount}] {e.Exception.GetType().Name}: {e.Exception.Message}");
            };

            var recordCount = 0;
            client.DataReceived += (sender, e) =>
            {
                recordCount++;
            };

            await client.SubscribeAsync(
                dataset: "EQUS.MINI",
                schema: Schema.Trades,
                symbols: new[] { "AAPL" }
            );

            var metadata = await client.StartAsync();
            Console.WriteLine($"✓ Stream started (Dataset: {metadata.Dataset})");
            Console.WriteLine();

            Console.WriteLine("Streaming for 5 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(5));

            Console.WriteLine();
            Console.WriteLine($"✓ Received {recordCount} records");
            Console.WriteLine($"✓ Errors handled via ErrorOccurred event: {eventErrorCount}");
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
        // Summary
        // ================================================================
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║  Summary: ExceptionCallback Feature                          ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("C++ API:");
        Console.WriteLine("  auto handler = [](const std::exception& e) {");
        Console.WriteLine("      return ExceptionAction::Continue;  // or Stop");
        Console.WriteLine("  };");
        Console.WriteLine("  client.Start(metadata_cb, record_cb, handler);");
        Console.WriteLine();
        Console.WriteLine("C# API:");
        Console.WriteLine("  var client = new LiveClientBuilder()");
        Console.WriteLine("      .WithExceptionHandler(ex => {");
        Console.WriteLine("          // Log error, send alert, etc.");
        Console.WriteLine("          return ExceptionAction.Continue;  // or Stop");
        Console.WriteLine("      })");
        Console.WriteLine("      .Build();");
        Console.WriteLine();
        Console.WriteLine("Use Cases:");
        Console.WriteLine("  • ExceptionAction.Continue - Log errors but keep streaming");
        Console.WriteLine("  • ExceptionAction.Stop     - Terminate on critical errors");
        Console.WriteLine("  • No handler               - Only ErrorOccurred event fires");
        Console.WriteLine();
        Console.WriteLine($"Test Results:");
        Console.WriteLine($"  • Test 1 (Continue): Handled {continueCount} error(s), continued streaming");
        Console.WriteLine($"  • Test 2 (Stop):     Handled {stopCount} error(s), stopped if > 0");
        Console.WriteLine("  • Test 3 (Default):  Used ErrorOccurred event only");
        Console.WriteLine();
    }
}
