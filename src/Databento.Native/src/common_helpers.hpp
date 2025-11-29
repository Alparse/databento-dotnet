#pragma once

#include <chrono>
#include <cstring>
#include <string>
#include <stdexcept>
#include <cstdio>
#include <databento/enums.hpp>
#include <databento/datetime.hpp>
#include <databento/log.hpp>

namespace databento_native {

/**
 * Safely copy a C string to a buffer with null termination
 *
 * MEDIUM FIX: Enhanced documentation and validation to prevent buffer overflow attacks
 *
 * CRITICAL SECURITY REQUIREMENT:
 * The dest_size parameter MUST exactly match the actual allocated buffer size!
 * Providing an incorrect size (larger than actual allocation) will cause buffer overflow.
 *
 * This function trusts the caller to provide accurate size information. It cannot detect
 * if the actual buffer is smaller than dest_size claims. The C# layer MUST validate
 * buffer sizes before calling native code.
 *
 * @param dest Destination buffer (must be at least dest_size bytes allocated)
 * @param dest_size ACTUAL size of destination buffer in bytes (NOT desired copy size!)
 * @param src Source string (can be nullptr for empty string)
 * @return true if copy succeeded, false if buffer invalid or too small
 *
 * Example CORRECT usage:
 *   char buffer[2048];
 *   SafeStrCopy(buffer, 2048, error_message);  // ✓ Size matches allocation
 *
 * Example INCORRECT usage:
 *   char buffer[100];
 *   SafeStrCopy(buffer, 1000, error_message);  // ✗ BUFFER OVERFLOW! Size is wrong
 */
inline bool SafeStrCopy(char* dest, size_t dest_size, const char* src) {
    // Validate destination pointer
    if (!dest) {
        return false;  // Cannot write to NULL
    }

    if (dest_size == 0) {
        return false;  // Cannot write to zero-size buffer
    }

    // MEDIUM FIX: Enforce reasonable minimum buffer size for error messages
    // Prevents uselessly small buffers that can't hold meaningful error messages
    constexpr size_t MIN_ERROR_BUFFER_SIZE = 16;
    if (dest_size < MIN_ERROR_BUFFER_SIZE) {
        // Still write what we can, but return false to indicate buffer too small
        if (src && src[0] != '\0') {
            #ifdef _WIN32
                strncpy_s(dest, dest_size, src, dest_size - 1);
            #else
                strncpy(dest, src, dest_size - 1);
                dest[dest_size - 1] = '\0';
            #endif
        } else {
            dest[0] = '\0';
        }
        return false;  // Indicate buffer too small for meaningful error
    }

    // MEDIUM FIX: Enforce maximum buffer size to prevent resource exhaustion
    // Caps extremely large buffers to prevent memory exhaustion attacks
    constexpr size_t MAX_ERROR_BUFFER_SIZE = 65536;  // 64KB (reasonable max for errors)
    if (dest_size > MAX_ERROR_BUFFER_SIZE) {
        // Log warning in debug builds
        #ifdef _DEBUG
        // In debug: Could add logging here if needed
        #endif
    }
    size_t safe_size = std::min(dest_size, MAX_ERROR_BUFFER_SIZE);

    // Handle null source (treat as empty string)
    if (!src) {
        dest[0] = '\0';
        return true;
    }

    // Copy with bounds checking
    // Use platform-specific safe string functions to avoid security warnings
    #ifdef _WIN32
        // Windows: Use strncpy_s which is the secure version
        strncpy_s(dest, safe_size, src, safe_size - 1);
    #else
        // Unix/Linux: Use standard strncpy with manual null termination
        strncpy(dest, src, safe_size - 1);
        dest[safe_size - 1] = '\0';  // Ensure null termination (defense in depth)
    #endif

    return true;
}

/**
 * Parse schema string to databento Schema enum
 * Centralized to ensure consistency across all wrappers
 * @param schema_str Schema string (e.g., "mbo", "mbp-1", "trades")
 * @return Schema enum value
 * @throws std::runtime_error if schema string is unknown
 */
inline databento::Schema ParseSchema(const std::string& schema_str) {
    // MBO/MBP schemas
    if (schema_str == "mbo") return databento::Schema::Mbo;
    if (schema_str == "mbp-1") return databento::Schema::Mbp1;
    if (schema_str == "mbp-10") return databento::Schema::Mbp10;

    // Trade schemas
    if (schema_str == "trades") return databento::Schema::Trades;
    if (schema_str == "tbbo") return databento::Schema::Tbbo;
    if (schema_str == "tcbbo") return databento::Schema::Tcbbo;

    // OHLCV schemas
    if (schema_str == "ohlcv-1s") return databento::Schema::Ohlcv1S;
    if (schema_str == "ohlcv-1m") return databento::Schema::Ohlcv1M;
    if (schema_str == "ohlcv-1h") return databento::Schema::Ohlcv1H;
    if (schema_str == "ohlcv-1d") return databento::Schema::Ohlcv1D;
    if (schema_str == "ohlcv-eod") return databento::Schema::OhlcvEod;

    // BBO schemas
    if (schema_str == "bbo-1s") return databento::Schema::Bbo1S;
    if (schema_str == "bbo-1m") return databento::Schema::Bbo1M;

    // Consolidated schemas
    if (schema_str == "cmbp-1") return databento::Schema::Cmbp1;
    if (schema_str == "cbbo-1s") return databento::Schema::Cbbo1S;
    if (schema_str == "cbbo-1m") return databento::Schema::Cbbo1M;

    // Other schemas
    if (schema_str == "definition") return databento::Schema::Definition;
    if (schema_str == "statistics") return databento::Schema::Statistics;
    if (schema_str == "status") return databento::Schema::Status;
    if (schema_str == "imbalance") return databento::Schema::Imbalance;

    // Unknown schema
    throw std::runtime_error("Unknown schema: " + schema_str);
}

/**
 * Convert nanoseconds since epoch to UnixNanos with validation
 * Prevents integer overflow from negative timestamps
 * @param ns Timestamp in nanoseconds since Unix epoch
 * @return UnixNanos value
 * @throws std::invalid_argument if timestamp is negative or too large
 */
inline databento::UnixNanos NsToUnixNanos(int64_t ns) {
    // Validate range - timestamps before Unix epoch not allowed
    if (ns < 0) {
        throw std::invalid_argument("Timestamp cannot be negative (before Unix epoch 1970-01-01)");
    }

    // CRITICAL FIX: Validate upper bound with realistic maximum
    // Year 2200-01-01 00:00:00 UTC in nanoseconds = 7,258,118,400,000,000,000
    // This is a reasonable practical limit (well before uint64_t overflow at year 2262)
    // Previous check against UINT64_MAX was ineffective as int64_t cast to uint64_t
    // can never exceed UINT64_MAX
    constexpr int64_t MAX_TIMESTAMP_NS = 7258118400000000000LL;  // Year 2200
    if (ns > MAX_TIMESTAMP_NS) {
        throw std::invalid_argument("Timestamp too large (after year 2200)");
    }

    // Safe cast to unsigned after validation
    return databento::UnixNanos{std::chrono::duration<uint64_t, std::nano>{static_cast<uint64_t>(ns)}};
}

/**
 * Validate that a string parameter is not NULL and not empty
 * @param param_name Name of the parameter for error messages
 * @param value String value to validate
 * @throws std::invalid_argument if validation fails
 */
inline void ValidateNonEmptyString(const char* param_name, const char* value) {
    if (!value) {
        throw std::invalid_argument(std::string(param_name) + " cannot be NULL");
    }
    if (value[0] == '\0') {
        throw std::invalid_argument(std::string(param_name) + " cannot be empty");
    }
}

/**
 * Validate symbol array parameters for consistency and prevent resource exhaustion
 * @param symbols Symbol array pointer
 * @param symbol_count Number of symbols
 * @throws std::invalid_argument if validation fails
 */
inline void ValidateSymbolArray(const char** symbols, size_t symbol_count) {
    // If count > 0, symbols array must not be NULL
    if (symbol_count > 0 && !symbols) {
        throw std::invalid_argument("Symbol array cannot be NULL when symbol_count > 0");
    }

    // HIGH FIX: Validate reasonable symbol count (prevent resource exhaustion)
    constexpr size_t MAX_SYMBOLS = 100000;  // Reasonable limit for batch operations
    if (symbol_count > MAX_SYMBOLS) {
        throw std::invalid_argument("Symbol count exceeds maximum limit of " + std::to_string(MAX_SYMBOLS));
    }

    // HIGH FIX: Validate individual symbol lengths and total size to prevent resource exhaustion
    // An attacker could provide 100,000 symbols where each is megabytes long
    constexpr size_t MAX_SYMBOL_LENGTH = 1024;  // Reasonable max for a ticker symbol
    constexpr size_t MAX_TOTAL_SIZE = 10 * 1024 * 1024;  // 10MB total for all symbols combined

    size_t total_size = 0;
    for (size_t i = 0; i < symbol_count; ++i) {
        // Check for NULL elements in array
        if (!symbols[i]) {
            throw std::invalid_argument("Symbol array contains NULL element at index " + std::to_string(i));
        }

        // Use strnlen to safely check length without reading past buffer
        // strnlen returns MAX_SYMBOL_LENGTH + 1 if string is longer than limit
        size_t len = strnlen(symbols[i], MAX_SYMBOL_LENGTH + 1);

        if (len > MAX_SYMBOL_LENGTH) {
            throw std::invalid_argument("Symbol at index " + std::to_string(i) +
                " exceeds maximum length of " + std::to_string(MAX_SYMBOL_LENGTH));
        }

        total_size += len;

        // Check running total to prevent overflow and resource exhaustion
        if (total_size > MAX_TOTAL_SIZE) {
            throw std::invalid_argument("Total symbol data size exceeds maximum limit of " +
                std::to_string(MAX_TOTAL_SIZE) + " bytes");
        }
    }
}

/**
 * Validate timestamp range
 * @param start_ns Start timestamp in nanoseconds
 * @param end_ns End timestamp in nanoseconds
 * @throws std::invalid_argument if validation fails
 */
inline void ValidateTimeRange(int64_t start_ns, int64_t end_ns) {
    // Both timestamps validated by NsToUnixNanos, just check ordering
    if (start_ns > end_ns) {
        throw std::invalid_argument("Start time must be before or equal to end time");
    }
}

/**
 * Validate error buffer parameters
 * @param error_buffer Error buffer pointer
 * @param error_buffer_size Error buffer size
 * @return true if error buffer is valid and can be used
 */
inline bool IsErrorBufferValid(char* error_buffer, size_t error_buffer_size) {
    return error_buffer != nullptr && error_buffer_size > 0;
}

// ============================================================================
// Shared Log Receiver for databento-cpp clients
// ============================================================================

/**
 * Simple ILogReceiver implementation that logs to stderr with level filtering
 * Used by all wrapper components to prevent NULL pointer dereferences
 * and provide consistent logging behavior across Historical, Batch, and Live clients.
 *
 * Design choices:
 * - stderr output: Doesn't interfere with application stdout
 * - Thread-safe: stderr writes are atomic for single fprintf calls
 * - Explicit flush: Ensures messages are visible immediately
 * - Consistent format: [Databento LEVEL] prefix for all messages
 * - Level filtering: Only logs messages at or above configured minimum level
 *
 * Log level severity (lowest to highest):
 *   Debug(0) < Info(1) < Warning(2) < Error(3)
 *
 * ============================================================================
 * DEPLOYMENT: CAPTURING STDERR LOGS
 * ============================================================================
 *
 * The native databento-cpp library outputs diagnostic logs to stderr. To capture
 * these logs in production, configure your deployment environment appropriately:
 *
 * 1. CONSOLE APPLICATIONS:
 *    Logs appear automatically on the console's stderr stream.
 *    Redirect with: myapp.exe 2>logs.txt  (Windows)
 *                   ./myapp 2>logs.txt    (Linux/macOS)
 *
 * 2. WINDOWS SERVICES:
 *    Use Event Log redirection or configure a log file:
 *    - In ServiceBase.OnStart(), redirect Console.Error to a StreamWriter
 *    - Or use ProcessStartInfo.RedirectStandardError when spawning processes
 *
 * 3. DOCKER/CONTAINERS:
 *    Container runtimes capture both stdout and stderr by default.
 *    Use: docker logs <container_id>
 *    Or configure logging driver to aggregate stderr output.
 *
 * 4. LINUX SYSTEMD:
 *    stderr is captured automatically in journald.
 *    View with: journalctl -u myservice.service
 *
 * 5. IIS/ASP.NET:
 *    Configure stdoutLogEnabled in web.config:
 *    <aspNetCore stdoutLogEnabled="true" stdoutLogFile=".\logs\stdout" />
 *    This captures both stdout and stderr to log files.
 *
 * 6. KUBERNETES:
 *    stderr is captured automatically by kubectl logs.
 *    Configure log aggregation (Fluentd, Loki, etc.) to collect stderr.
 *
 * CONFIGURING LOG LEVEL:
 *    Use dbento_live_set_log_level(), dbento_live_blocking_set_log_level(),
 *    or dbento_historical_set_log_level() to filter output:
 *    - Level 0 (Debug): All messages including verbose debug output
 *    - Level 1 (Info): Informational messages and above (default)
 *    - Level 2 (Warning): Warning messages and errors only
 *    - Level 3 (Error): Only error messages
 *
 * ============================================================================
 */
class StderrLogReceiver : public databento::ILogReceiver {
public:
    /**
     * Construct StderrLogReceiver with configurable minimum log level
     * @param min_level Minimum level to log (default: Info - logs Info, Warning, Error)
     */
    explicit StderrLogReceiver(databento::LogLevel min_level = databento::LogLevel::Info)
        : min_level_(min_level) {}

    /**
     * Set the minimum log level
     * @param level Minimum level to log (messages below this level are filtered out)
     */
    void SetMinLevel(databento::LogLevel level) {
        min_level_ = level;
    }

    /**
     * Get the current minimum log level
     * @return Current minimum log level
     */
    databento::LogLevel GetMinLevel() const {
        return min_level_;
    }

    /**
     * Check if a message at the given level should be logged
     * @param level Log level to check
     * @return true if level >= min_level_, false otherwise
     */
    bool ShouldLog(databento::LogLevel level) const override {
        // Log levels: Debug=0, Info=1, Warning=2, Error=3
        // We log if level >= min_level (e.g., if min_level=Warning, we log Warning and Error)
        return static_cast<int>(level) >= static_cast<int>(min_level_);
    }

    void Receive(databento::LogLevel level, const std::string& message) override {
        // Filter by minimum level
        if (!ShouldLog(level)) {
            return;
        }

        const char* level_str = "INFO";
        switch (level) {
            case databento::LogLevel::Error:   level_str = "ERROR";   break;
            case databento::LogLevel::Warning: level_str = "WARNING"; break;
            case databento::LogLevel::Info:    level_str = "INFO";    break;
            case databento::LogLevel::Debug:   level_str = "DEBUG";   break;
        }

        // Write to stderr with explicit flush for reliability
        // Format: [Databento LEVEL] message
        std::fprintf(stderr, "[Databento %s] %s\n", level_str, message.c_str());
        std::fflush(stderr);
    }

private:
    databento::LogLevel min_level_;
};

}  // namespace databento_native
