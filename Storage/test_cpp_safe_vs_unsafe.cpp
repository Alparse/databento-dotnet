// Demonstrates SAFE vs UNSAFE ways to create databento-cpp Historical client
// This shows why Builder pattern works but direct constructor crashes

#include <databento/historical.hpp>
#include <databento/exceptions.hpp>
#include <iostream>
#include <cstdlib>

void test_safe_builder() {
    std::cout << "=== TEST 1: SAFE - Using Builder Pattern ===" << std::endl;

    try {
        // Builder automatically provides ILogReceiver::Default() if not specified
        auto client = databento::HistoricalBuilder()
            .SetKeyFromEnv()
            .Build();

        std::cout << "✓ Client created with Builder (safe)" << std::endl;

        // Query future dates - will trigger warning
        databento::DateTimeRange<databento::UnixNanos> date_range{
            databento::UnixNanos{1746057600000000000LL},  // 2025-05-01
            databento::UnixNanos{1763884800000000000LL}   // 2025-11-18
        };

        int count = 0;
        client.TimeseriesGetRange(
            "GLBX.MDP3",
            date_range,
            {"ES.FUT"},
            databento::Schema::Ohlcv1D,
            [&count](const databento::Record&) {
                count++;
                return databento::KeepGoing::Continue;
            }
        );

        std::cout << "✓ SUCCESS: Received " << count << " records" << std::endl;
        std::cout << "  Builder pattern is SAFE - no crash!" << std::endl;
        std::cout << std::endl;

    } catch (const std::exception& e) {
        std::cout << "Exception: " << e.what() << std::endl;
    }
}

void test_unsafe_direct_constructor() {
    std::cout << "=== TEST 2: UNSAFE - Direct Constructor with nullptr ===" << std::endl;

    try {
        const char* api_key_env = std::getenv("DATABENTO_API_KEY");
        if (!api_key_env) {
            std::cout << "ERROR: DATABENTO_API_KEY not set" << std::endl;
            return;
        }

        // Direct constructor with nullptr - NO SAFETY NET!
        auto client = databento::Historical(
            nullptr,  // ❌ DANGER: Will crash if warning is logged!
            std::string(api_key_env),
            databento::HistoricalGateway::Bo1
        );

        std::cout << "✓ Client created with direct constructor (dangerous)" << std::endl;
        std::cout << "  (No crash yet because we haven't triggered a warning)" << std::endl;

        // Query future dates - CRASH EXPECTED HERE
        databento::DateTimeRange<databento::UnixNanos> date_range{
            databento::UnixNanos{1746057600000000000LL},  // 2025-05-01
            databento::UnixNanos{1763884800000000000LL}   // 2025-11-18
        };

        std::cout << "Starting query that will trigger warning..." << std::endl;
        std::cout << "💥 EXPECTED: Access violation crash here!" << std::endl;
        std::cout << std::endl;

        int count = 0;
        client.TimeseriesGetRange(
            "GLBX.MDP3",
            date_range,
            {"ES.FUT"},
            databento::Schema::Ohlcv1D,
            [&count](const databento::Record&) {
                count++;
                return databento::KeepGoing::Continue;
            }
        );

        // If we reach here, bug is fixed!
        std::cout << "✓ UNEXPECTED SUCCESS: Received " << count << " records" << std::endl;
        std::cout << "  (databento-cpp must have added null checks!)" << std::endl;

    } catch (const std::exception& e) {
        std::cout << "✓ Exception caught: " << e.what() << std::endl;
        std::cout << "  (Better than crash, but still not ideal)" << std::endl;
    }
}

int main() {
    std::cout << "=== databento-cpp nullptr Safety Test ===" << std::endl;
    std::cout << std::endl;

    // Test 1: Safe way (Builder)
    test_safe_builder();

    std::cout << "========================================" << std::endl;
    std::cout << std::endl;

    // Test 2: Unsafe way (Direct constructor)
    // WARNING: This will likely crash the program!
    std::cout << "⚠️  WARNING: Next test will likely CRASH!" << std::endl;
    std::cout << "Press Ctrl+C to abort, or Enter to continue..." << std::endl;
    std::cin.get();

    test_unsafe_direct_constructor();

    return 0;
}

/*
EXPECTED OUTPUT (with bug):
===========================

=== TEST 1: SAFE - Using Builder Pattern ===
✓ Client created with Builder (safe)
✓ SUCCESS: Received 172 records
  Builder pattern is SAFE - no crash!

========================================

⚠️  WARNING: Next test will likely CRASH!
Press Ctrl+C to abort, or Enter to continue...

=== TEST 2: UNSAFE - Direct Constructor with nullptr ===
✓ Client created with direct constructor (dangerous)
  (No crash yet because we haven't triggered a warning)
Starting query that will trigger warning...
💥 EXPECTED: Access violation crash here!

[PROGRAM CRASHES - No exception caught, immediate termination]
Access violation reading location 0x0000000000000000


EXPECTED OUTPUT (if fixed):
===========================

Both tests should succeed without crashes!
*/
