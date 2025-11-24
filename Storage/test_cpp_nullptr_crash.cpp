// Minimal C++ reproduction of the nullptr crash bug
// This demonstrates the bug exists in databento-cpp itself, not just our .NET wrapper

#include <databento/historical.hpp>
#include <databento/exceptions.hpp>
#include <iostream>
#include <cstdlib>

int main() {
    std::cout << "=== C++ nullptr Crash Reproduction ===" << std::endl;
    std::cout << "Testing Historical client with nullptr ILogReceiver" << std::endl;
    std::cout << std::endl;

    try {
        // Get API key from environment
        const char* api_key_env = std::getenv("DATABENTO_API_KEY");
        if (!api_key_env) {
            std::cerr << "ERROR: DATABENTO_API_KEY environment variable not set" << std::endl;
            return 1;
        }
        std::string api_key(api_key_env);

        std::cout << "✓ API key found" << std::endl;

        // === THIS IS THE BUG ===
        // Direct constructor with nullptr for ILogReceiver
        // Builder pattern provides safe default, but direct constructor doesn't check!
        std::cout << "Creating Historical client with nullptr ILogReceiver..." << std::endl;

        auto client = databento::Historical(
            nullptr,  // ❌ BUG: No null check in databento-cpp!
            api_key,
            databento::HistoricalGateway::Bo1
        );

        std::cout << "✓ Client created (no crash yet)" << std::endl;
        std::cout << std::endl;

        // Query future dates - this will trigger X-Warning header from server
        std::cout << "Querying future dates (will trigger server warning)..." << std::endl;

        std::string dataset = "GLBX.MDP3";
        std::vector<std::string> symbols = {"ES.FUT"};
        databento::Schema schema = databento::Schema::Ohlcv1D;

        // May 1, 2025 to Nov 18, 2025 (future dates with degraded quality)
        databento::DateTimeRange<databento::UnixNanos> date_range{
            databento::UnixNanos{1746057600000000000LL},  // 2025-05-01
            databento::UnixNanos{1763884800000000000LL}   // 2025-11-18
        };

        int record_count = 0;

        // === CRASH HAPPENS HERE ===
        // When server returns X-Warning header, databento-cpp tries to log it:
        // log_receiver->Receive(LogLevel::Warning, message)
        // Since log_receiver is nullptr -> ACCESS VIOLATION!

        std::cout << "Starting query..." << std::endl;
        client.TimeseriesGetRange(
            dataset,
            date_range,
            symbols,
            schema,
            [&record_count](const databento::Record& record) {
                record_count++;
                if (record_count <= 5) {
                    std::cout << "  Record " << record_count << " received" << std::endl;
                }
                return databento::KeepGoing::Continue;
            }
        );

        // If we reach here, the bug might be fixed in newer versions
        std::cout << std::endl;
        std::cout << "✓ SUCCESS: Received " << record_count << " records without crash!" << std::endl;
        std::cout << "  (This means databento-cpp has been fixed to handle nullptr safely)" << std::endl;
        return 0;

    } catch (const std::exception& e) {
        // If we catch this, it's a proper exception (good)
        std::cout << std::endl;
        std::cout << "✓ CAUGHT EXCEPTION: " << e.what() << std::endl;
        std::cout << "  (This is better than a crash)" << std::endl;
        return 1;

    } catch (...) {
        // Shouldn't reach here unless something really bad happened
        std::cout << std::endl;
        std::cout << "✗ CAUGHT UNKNOWN EXCEPTION" << std::endl;
        return 1;
    }
}

// Expected behavior (BUG):
//   1. Client created successfully ✓
//   2. Query starts...
//   3. Server returns X-Warning header (future dates)
//   4. databento-cpp tries: log_receiver->Receive(...)
//   5. CRASH: Access violation at 0x0000000000000000
//   6. Program terminates immediately (no exception caught)
//
// Desired behavior (FIX):
//   1. Check if log_receiver is nullptr before dereferencing
//   2. Either skip logging or use stderr as fallback
//   3. Continue processing data normally
