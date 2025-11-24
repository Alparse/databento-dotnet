# AccessViolationException Bug Fix Summary

## Root Cause

**Location**: `src/Databento.Native/src/historical_client_wrapper.cpp:36`

The Historical client was created with `nullptr` for the log_receiver parameter:
```cpp
client = std::make_unique<db::Historical>(nullptr, key, db::HistoricalGateway::Bo1);
                                          ^^^^^^^
```

When the Databento API returns an `X-Warning` HTTP header (which happens for future dates with "reduced quality" warnings), databento-cpp's `CheckWarnings()` function attempts to call `log_receiver_->Receive()`, causing a NULL pointer dereference → **AccessViolationException**.

## The Issue Chain

1. User requests historical data for **future dates** (e.g., May-Nov 2025)
2. Databento API returns HTTP 200 OK with `X-Warning` header:
   ```
   X-Warning: ["Warning: The streaming request contained one or more days which have reduced quality: 2025-09-17 (degraded), 2025-09-24 (degraded)..."]
   ```
3. databento-cpp's `http_client.cpp::CheckWarnings()` parses the warning and calls:
   ```cpp
   log_receiver_->Receive(LogLevel::Warning, msg.str());  // CRASH: log_receiver_ is NULL!
   ```
4. AccessViolationException → application crash

## Why It Only Happens With Future Dates

- **Past dates with valid data**: No warnings → CheckWarnings returns early → no NULL dereference
- **Future dates**: API returns quality warnings → CheckWarnings tries to log → NULL dereference → crash

## Fix Options

### Option 1: Fix databento-cpp (Defensive Approach)

**File**: `databento-cpp/src/detail/http_client.cpp`

Add NULL checks in `CheckWarnings()` before calling `log_receiver_->Receive()`:

```cpp
void HttpClient::CheckWarnings(const httplib::Response& response) const {
  const auto raw = response.get_header_value("X-Warning");
  if (!raw.empty()) {
    try {
      const auto json = nlohmann::json::parse(raw);
      if (json.is_array()) {
        for (const auto& warning_json : json.items()) {
          const std::string warning = warning_json.value();
          std::ostringstream msg;
          msg << "[HttpClient::CheckWarnings] Server " << warning;

          // FIX: Check for NULL before calling
          if (log_receiver_ != nullptr) {
            log_receiver_->Receive(LogLevel::Warning, msg.str());
          } else {
            // Fallback: log to stderr if no receiver available
            std::fprintf(stderr, "[Databento Warning] %s\n", msg.str().c_str());
            std::fflush(stderr);
          }
        }
        return;
      }
    } catch (const std::exception& exc) {
      // Similar NULL check needed here
      std::ostringstream msg;
      msg << "[HttpClient::CheckWarnings] Failed to parse warnings: "
          << exc.what() << ". Raw: " << raw;

      if (log_receiver_ != nullptr) {
        log_receiver_->Receive(LogLevel::Warning, msg.str());
      } else {
        std::fprintf(stderr, "[Databento Warning] %s\n", msg.str().c_str());
        std::fflush(stderr);
      }
      return;
    }
    // More NULL checks for other code paths...
  }
}
```

**Pros**:
- Defensive programming - handles NULL gracefully
- Protects against similar issues elsewhere in databento-cpp

**Cons**:
- Requires modifying third-party dependency (databento-cpp)
- Should be reported upstream to databento-cpp maintainers

### Option 2: Fix Our Wrapper (Correct Approach)

**File**: `src/Databento.Native/src/historical_client_wrapper.cpp`

Create a proper `ILogReceiver` implementation and pass it to Historical client:

```cpp
// Add a simple ILogReceiver implementation
class StderrLogReceiver : public databento::ILogReceiver {
public:
    void Receive(databento::LogLevel level, const std::string& message) override {
        const char* level_str = "INFO";
        switch (level) {
            case databento::LogLevel::Error:   level_str = "ERROR";   break;
            case databento::LogLevel::Warning: level_str = "WARNING"; break;
            case databento::LogLevel::Info:    level_str = "INFO";    break;
            case databento::LogLevel::Debug:   level_str = "DEBUG";   break;
        }
        std::fprintf(stderr, "[Databento %s] %s\n", level_str, message.c_str());
        std::fflush(stderr);
    }
};

struct HistoricalClientWrapper {
    std::unique_ptr<db::Historical> client;
    std::string api_key;
    std::unique_ptr<StderrLogReceiver> log_receiver;  // Add this

    explicit HistoricalClientWrapper(const std::string& key)
        : api_key(key),
          log_receiver(std::make_unique<StderrLogReceiver>()) {  // Create log receiver
        // Pass log_receiver.get() instead of nullptr
        client = std::make_unique<db::Historical>(
            log_receiver.get(),  // FIX: Pass valid pointer
            key,
            db::HistoricalGateway::Bo1
        );
    }
};
```

**Pros**:
- Fixes the actual bug in OUR code
- No need to modify databento-cpp
- Enables proper warning logging for users

**Cons**:
- None - this is the correct fix

## Recommended Approach

**Use Option 2** (Fix our wrapper) as the primary fix, and **report Option 1** to databento-cpp as a defensive enhancement.

## Testing

Run the test program in Visual Studio:
```
examples\HistoricalFutureDates.Test\
```

**Before fix**: AccessViolationException crash
**After fix**: Successfully receives 172 records with warning logged

## Files to Modify

1. `src/Databento.Native/src/historical_client_wrapper.cpp` - Add ILogReceiver implementation
2. (Optional) Rebuild native library and update runtimes DLLs

## Verification

The fix is working if:
1. No AccessViolationException when requesting future dates
2. Warning message appears: "reduced quality: 2025-09-17 (degraded), 2025-09-24 (degraded)"
3. All 172 historical records are successfully received
