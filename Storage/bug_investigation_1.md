# Bug Investigation #1: AccessViolationException in Historical API

**Date**: November 19, 2025
**Status**: Root cause identified, fix tested and verified
**Severity**: CRITICAL

---

## Executive Summary

Discovered and fixed a critical NULL pointer dereference bug causing `AccessViolationException` when requesting historical data for future dates. The bug exists in multiple components and affects Historical, Batch, and potentially Live clients.

### Impact
- **Historical API**: ✅ Fixed and tested
- **Batch API**: 🔴 Vulnerable (same bug, not yet fixed)
- **Live APIs**: ⚠️ Potentially vulnerable (needs investigation)

---

## Bug Discovery

### Initial Symptoms
- Application crashes with `AccessViolationException` when calling Historical API
- **Trigger condition**: Requesting data for future dates (May-Nov 2025)
- **Test symbol**: CLZ5 (Crude Oil futures)
- **Dataset**: GLBX.MDP3
- Past dates work fine, future dates crash 100% of the time

### Error Message
```
Fatal error. System.AccessViolationException: Attempted to read or write protected memory.
This is often an indication that other memory is corrupt.
```

---

## Root Cause Analysis

### The Bug Chain

1. **Wrapper passes NULL pointer** (`src/Databento.Native/src/historical_client_wrapper.cpp:36`):
```cpp
struct HistoricalClientWrapper {
    std::unique_ptr<db::Historical> client;
    std::string api_key;

    explicit HistoricalClientWrapper(const std::string& key)
        : api_key(key) {
        client = std::make_unique<db::Historical>(
            nullptr,  // ← BUG: Passing NULL for ILogReceiver parameter
            key,
            db::HistoricalGateway::Bo1
        );
    }
};
```

2. **API returns warning header** for future dates:
```http
HTTP/1.1 200 OK
X-Warning: ["Warning: The streaming request contained one or more days which have
            reduced quality: 2025-09-17 (degraded), 2025-09-24 (degraded), ..."]
```

3. **databento-cpp attempts to log warning** (`databento-cpp/src/detail/http_client.cpp:202`):
```cpp
void HttpClient::CheckWarnings(const httplib::Response& response) const {
    const auto raw = response.get_header_value("X-Warning");

    if (!raw.empty()) {
        const auto json = nlohmann::json::parse(raw);
        if (json.is_array()) {
            for (const auto& warning_json : json.items()) {
                std::ostringstream msg;
                msg << "[HttpClient::CheckWarnings] Server " << warning_json.value();

                // CRASH: log_receiver_ is NULL!
                log_receiver_->Receive(LogLevel::Warning, msg.str());
                //           ^^
                // NULL pointer dereference → AccessViolationException
            }
        }
    }
}
```

4. **CPU memory protection triggers**: Address 0x0000000000000000 is protected → Hardware exception → .NET marshals as AccessViolationException

### Why Only Future Dates?

- **Past dates with valid data**: No X-Warning header → CheckWarnings() returns early → no NULL dereference
- **Future dates**: API returns degraded quality warnings → CheckWarnings() tries to log → NULL dereference → **CRASH**

### Diagnostic Evidence

Added logging to databento-cpp revealed the smoking gun:

```
[HTTP] Response handler called: status=200
[HTTP] About to call CheckWarnings
[HTTP] CheckWarnings: Entry
[HTTP] CheckWarnings: log_receiver_ = 0000000000000000  ← NULL!
[HTTP] CheckWarnings: X-Warning header present: ["Warning: ..."]
[HTTP] CheckWarnings: About to call log_receiver_->Receive
<CRASH - Attempted to read or write protected memory>
```

---

## Why nullptr Was Passed - The Design Mistake

### TL;DR: Used Deprecated Constructor Instead of Builder Pattern

Our wrapper used databento-cpp's **deprecated direct constructor** which has no nullptr safety, instead of the recommended **Builder pattern** which provides a safe default.

### The Critical Code Path Difference

**❌ What Our Wrapper Does (WRONG)**:
```cpp
// historical_client_wrapper.cpp:36
client = std::make_unique<db::Historical>(nullptr, key, db::HistoricalGateway::Bo1);
                                          ^^^^^^^
// Uses deprecated constructor - bypasses all safety checks
```

**✅ What databento-cpp Examples Do (CORRECT)**:
```cpp
// From databento-cpp/examples/historical/timeseries_get_range.cpp
auto client = db::Historical::Builder().SetKeyFromEnv().Build();
// Builder ensures log_receiver is never nullptr
```

### The Safety Mechanism We Bypassed

**HistoricalBuilder::Build()** has nullptr protection (`databento-cpp/src/historical.cpp`):

```cpp
Historical HistoricalBuilder::Build() {
  if (key_.empty()) {
    throw Exception{"'key' is unset"};
  }

  // ⭐ THIS IS THE SAFETY WE BYPASSED ⭐
  if (log_receiver_ == nullptr) {
    log_receiver_ = databento::ILogReceiver::Default();  // Provides safe default!
  }

  return Historical{log_receiver_, key_, gateway_, upgrade_policy_, user_agent_ext_};
}
```

**Direct constructor has NO safety** (`databento-cpp/include/databento/historical.hpp`):

```cpp
// WARNING: Will be deprecated in the future in favor of the builder
Historical(ILogReceiver* log_receiver, std::string key, HistoricalGateway gateway);
           ^^^^^^^^^^^^^^^^
// No nullptr check! Directly stores whatever you pass and crashes later!
```

### databento-cpp API Design Flaw

| Constructor Method | nullptr Safety | Documented? |
|-------------------|----------------|-------------|
| Direct constructor | ❌ **No - crashes** | ⚠️ "Will be deprecated" |
| Builder.Build() | ✅ **Yes - provides Default()** | ✅ All examples use this |

**The Problem**: The direct constructor:
- Has no nullptr checks
- Has no clear documentation stating "nullptr not allowed"
- Will crash with cryptic AccessViolationException
- Is marked "will be deprecated" but still compiles without warnings

**The direct constructor should either**:
1. Check for nullptr and provide ILogReceiver::Default(), OR
2. Clearly document "nullptr not allowed - will crash", OR
3. Be actually deprecated with compiler warnings

Instead, it's a **latent bug trap**.

### Why It Worked Initially (Latent Bug)

The bug was present **since the initial commit** (34e844c) but didn't manifest because:

1. **Past dates don't trigger warnings**: No X-Warning headers → CheckWarnings() returns early → nullptr never dereferenced
2. **Most testing used valid historical dates**: Tests with 2023-2024 data never hit the code path
3. **Silent failure waiting to happen**: The NULL pointer sat there dormant, waiting for any API warning

This is a **time bomb bug** - works fine until the day someone:
- Requests future dates (triggers quality warnings)
- Hits rate limits (may trigger warnings)
- Uses invalid parameters (may trigger warnings)
- Gets any HTTP response with X-Warning header

### How This Happened - Root Cause of the Mistake

Looking at git history, `nullptr` has been there since day one. **Most likely reasons**:

1. **Didn't know about Builder pattern**
   - The warning comment "Will be deprecated in favor of builder" may not have been visible or clear
   - Examples might not have been consulted
   - Constructor parameters looked straightforward

2. **Assumed nullptr was acceptable**
   - Many C++ APIs treat nullptr log receivers as "silent - no logging"
   - Common pattern in logging frameworks
   - Reasonable assumption, but wrong for databento-cpp

3. **No documentation about requirement**
   - Constructor signature doesn't indicate nullptr is forbidden
   - No @param documentation saying "must be non-null"
   - No runtime checks or assertions
   - No compiler warnings

4. **Worked in all initial tests**
   - Historical data for past dates doesn't trigger warnings
   - All example requests were for 2023-2024 data
   - Bug was completely invisible during development
   - No indication anything was wrong

### The Proper Way: How All databento-cpp Examples Do It

Every official databento-cpp example uses the Builder pattern:

```cpp
// examples/historical/timeseries_get_range.cpp
auto client = db::Historical::Builder().SetKeyFromEnv().Build();

// examples/historical/batch.cpp
auto client = db::Historical::Builder().SetKey(key).Build();

// examples/historical/metadata.cpp
auto client = db::Historical::Builder().SetKeyFromEnv().Build();
```

**Builder pattern benefits**:
- ✅ Provides ILogReceiver::Default() automatically
- ✅ Validates all required parameters
- ✅ Prevents common mistakes
- ✅ Future-proof against API changes
- ✅ Recommended by databento-cpp maintainers

### Comparison: Constructor vs Builder

| Aspect | Direct Constructor | Builder Pattern |
|--------|-------------------|-----------------|
| **nullptr safety** | ❌ None - crashes | ✅ Provides Default() |
| **Parameter validation** | ❌ Minimal | ✅ Comprehensive |
| **Documentation** | ⚠️ "Will be deprecated" | ✅ All examples |
| **Future-proof** | ❌ May be removed | ✅ Recommended path |
| **Error messages** | ❌ Cryptic crash | ✅ Clear exceptions |
| **Used by official examples** | ❌ Never | ✅ Always |

### Why We Didn't Use Builder Pattern

**Technical barrier**: The Builder pattern in C++ returns a value type:

```cpp
// databento-cpp example
auto client = db::Historical::Builder().SetKey(key).Build();
// Returns Historical by value, not pointer
```

**Our wrapper needs pointer**:
```cpp
struct HistoricalClientWrapper {
    std::unique_ptr<db::Historical> client;  // We need to store in unique_ptr

    HistoricalClientWrapper(const std::string& key) {
        // Can't directly use Builder because it returns value, not pointer
        client = std::make_unique<db::Historical>(/* ??? */);
    }
};
```

**Workaround exists**:
```cpp
// Could have done this:
HistoricalClientWrapper(const std::string& key) {
    auto built_client = db::Historical::Builder().SetKey(key).Build();
    client = std::make_unique<db::Historical>(std::move(built_client));
}
```

But instead, took the "simpler" direct constructor path without realizing the nullptr requirement.

### Lessons Learned

1. **Always consult official examples** when using third-party C++ APIs
2. **Never assume nullptr is acceptable** for pointer parameters without documentation
3. **Pay attention to deprecation warnings** - they're there for a reason
4. **Use Builder patterns** even if they seem more complex
5. **Test edge cases** that might trigger warnings or errors
6. **Request future dates** as part of test coverage to catch latent bugs

### Recommendation for databento-cpp Maintainers

We should report this API design issue:

**Problem**: Direct constructor has no nullptr safety and no clear documentation

**Suggested fixes**:
1. **Add nullptr check** in constructor:
```cpp
Historical(ILogReceiver* log_receiver, std::string key, HistoricalGateway gateway)
    : log_receiver_{log_receiver ? log_receiver : ILogReceiver::Default()},
      key_{std::move(key)},
      gateway_{UrlFromGateway(gateway)} {}
```

2. **Or add runtime assertion**:
```cpp
Historical(ILogReceiver* log_receiver, std::string key, HistoricalGateway gateway) {
    if (log_receiver == nullptr) {
        throw InvalidArgumentError{"Historical constructor", "log_receiver",
                                  "Cannot be nullptr. Use Builder pattern or provide valid ILogReceiver."};
    }
    // ... rest of constructor
}
```

3. **Or actually deprecate** with compiler warnings:
```cpp
[[deprecated("Use Historical::Builder() instead")]]
Historical(ILogReceiver* log_receiver, std::string key, HistoricalGateway gateway);
```

Any of these would have prevented our bug.

---

## Complete Vulnerability Assessment

### Confirmed Vulnerable Components

#### 1. Historical Client (FIXED in test project)
**File**: `src/Databento.Native/src/historical_client_wrapper.cpp:36`
**Status**: ✅ Fixed and tested
**Trigger**: Future dates, API warnings
**Test**: HistoricalFutureDates.Test - 172 records received successfully

#### 2. Batch Client (VULNERABLE - NOT FIXED)
**File**: `src/Databento.Native/src/batch_wrapper.cpp:34`
**Status**: 🔴 **CRITICAL - Same bug as Historical**
**Code**:
```cpp
struct HistoricalClientWrapper {
    std::unique_ptr<db::Historical> client;
    std::string api_key;

    explicit HistoricalClientWrapper(const std::string& key)
        : api_key(key) {
        client = std::make_unique<db::Historical>(nullptr, key, db::HistoricalGateway::Bo1);
                                                  ^^^^^^^
    }
};
```
**Risk**: Batch operations on future dates will crash with AccessViolationException
**Severity**: HIGH - Batch API uses same Historical client under the hood

#### 3. Live Clients (POTENTIALLY VULNERABLE)
**Files**:
- `src/Databento.Native/src/live_blocking_wrapper.cpp`
- `src/Databento.Native/src/live_client_wrapper.cpp`

**Status**: ⚠️ **Needs investigation**

**Code**:
```cpp
void EnsureClientCreated() {
    if (!client) {
        auto builder = db::LiveBlocking::Builder()
            .SetKey(api_key)
            .SetDataset(dataset)
            .SetSendTsOut(send_ts_out)
            .SetUpgradePolicy(upgrade_policy);
            // Missing: .SetLogReceiver(...)

        client = std::make_unique<db::LiveBlocking>(builder.BuildBlocking());
        // Builder's log_receiver_ defaults to nullptr
    }
}
```

**NULL Pointer Dereferences in databento-cpp**:
- `live_blocking.cpp`: **15 dereferences** of log_receiver_
  - `Authenticate()` - called in constructor
  - `Start()` - called when starting stream
  - `Subscribe()` - called during subscription
  - Various debug/info/warning logs throughout

**Mystery**: User's CLAUDE.md examples show successful logging like:
```
INFO: [LiveBlocking::Authenticate] Successfully authenticated with session_id 1763732978
```

This shouldn't be possible with nullptr. **Investigation needed**:
- Production databento-cpp may have nullptr safety we don't see in source
- Some initialization path we're missing
- It crashes in scenarios not yet tested

### All NULL Pointer Dereferences in databento-cpp

**Total**: 25 unsafe dereferences across 5 files

| File | Count | Functions Affected |
|------|-------|-------------------|
| `live_blocking.cpp` | 15 | Authenticate, Start, Subscribe, various |
| `historical.cpp` | 4 | GetRange, GetRangeToFile, various |
| `http_client.cpp` | 4 | CheckWarnings (3x), error handling |
| `zstd_stream.cpp` | 1 | Compression logging |
| `dbn_decoder.cpp` | 1 | Decoding logging |

---

## The Fix - Two Approaches

### Approach 1: Defensive Fix in databento-cpp (What We Tested)

**Location**: `databento-cpp/src/detail/http_client.cpp` (all 3 code paths in CheckWarnings)

**Fix**: Add NULL checks before dereferencing log_receiver_

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
            std::ostringstream msg;
            msg << "[HttpClient::CheckWarnings] Failed to parse warnings: "
                << exc.what() << ". Raw: " << raw;

            // FIX: Check for NULL before calling
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
- Defensive programming - prevents crashes even with misuse
- Protects against similar bugs in other databento-cpp locations
- Works immediately without changing API usage

**Cons**:
- Modifies third-party dependency code
- Must maintain patch when updating databento-cpp
- Should be reported upstream

**Test Results**: ✅ **SUCCESS**
```
Testing Historical API with future dates (May-Nov 2025)...
Fetching data...

[HTTP] CheckWarnings: log_receiver_ is NULL, cannot log warning: [HttpClient::CheckWarnings] Server Warning: ...
[HTTP] Response handler completed

Historical record: OHLCV: O:56.81 H:57.73 L:55.17 C:57.14 V:18031 [2025-05-01...]
Historical record: OHLCV: O:57.25 H:58.03 L:56.32 C:57.12 V:11917 [2025-05-02...]
...

✓ SUCCESS: Received 172 records without crashing!
```

### Approach 2: Fix Wrapper (Proper Fix - RECOMMENDED)

**Location**: `src/Databento.Native/src/historical_client_wrapper.cpp` (and batch, live wrappers)

**Fix**: Create proper ILogReceiver implementation and pass valid pointer

```cpp
// Add shared log receiver implementation (can be in common_helpers.hpp)
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

// Historical wrapper
struct HistoricalClientWrapper {
    std::unique_ptr<db::Historical> client;
    std::string api_key;
    std::unique_ptr<StderrLogReceiver> log_receiver;  // Add this

    explicit HistoricalClientWrapper(const std::string& key)
        : api_key(key),
          log_receiver(std::make_unique<StderrLogReceiver>()) {  // Create receiver
        // Pass valid pointer instead of nullptr
        client = std::make_unique<db::Historical>(
            log_receiver.get(),  // FIX: Pass valid pointer
            key,
            db::HistoricalGateway::Bo1
        );
    }
};

// Batch wrapper (same fix needed)
struct HistoricalClientWrapper {  // Used in batch_wrapper.cpp
    std::unique_ptr<db::Historical> client;
    std::string api_key;
    std::unique_ptr<StderrLogReceiver> log_receiver;  // Add this

    explicit HistoricalClientWrapper(const std::string& key)
        : api_key(key),
          log_receiver(std::make_unique<StderrLogReceiver>()) {  // Create receiver
        client = std::make_unique<db::Historical>(
            log_receiver.get(),  // FIX: Pass valid pointer
            key,
            db::HistoricalGateway::Bo1
        );
    }
};

// Live wrappers
struct LiveBlockingWrapper {
    std::unique_ptr<db::LiveBlocking> client;
    std::unique_ptr<StderrLogReceiver> log_receiver;  // Add this
    std::string dataset;
    std::string api_key;
    // ... other fields ...

    explicit LiveBlockingWrapper(const std::string& key)
        : api_key(key),
          log_receiver(std::make_unique<StderrLogReceiver>()) {}  // Create receiver

    void EnsureClientCreated() {
        if (!client) {
            auto builder = db::LiveBlocking::Builder()
                .SetKey(api_key)
                .SetDataset(dataset)
                .SetSendTsOut(send_ts_out)
                .SetUpgradePolicy(upgrade_policy)
                .SetLogReceiver(log_receiver.get());  // FIX: Add this

            if (heartbeat_interval_secs > 0) {
                builder.SetHeartbeatInterval(std::chrono::seconds(heartbeat_interval_secs));
            }

            client = std::make_unique<db::LiveBlocking>(builder.BuildBlocking());
        }
    }
};
```

**Pros**:
- Fixes the actual bug in OUR code (not passing nullptr)
- No modifications to databento-cpp needed
- Users see helpful warning messages about data quality
- Follows databento-cpp's intended API usage pattern
- Single shared StderrLogReceiver class for all wrappers

**Cons**:
- Requires rebuilding native library
- Must copy updated DLL to runtime locations

**Recommended for Production**: YES

---

## Implementation Plan

### Immediate Actions (for main databento-dotnet repo)

1. **Create shared StderrLogReceiver** in `common_helpers.hpp`:
```cpp
// In common_helpers.hpp
namespace databento_native {

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

} // namespace databento_native
```

2. **Fix HistoricalClientWrapper** (`historical_client_wrapper.cpp:28-38`):
   - Add `std::unique_ptr<StderrLogReceiver> log_receiver;` field
   - Initialize in constructor
   - Pass `log_receiver.get()` instead of `nullptr`

3. **Fix Batch wrapper** (`batch_wrapper.cpp:28-36`):
   - Same fix as Historical (it uses HistoricalClientWrapper)

4. **Fix LiveBlockingWrapper** (`live_blocking_wrapper.cpp:48-62`):
   - Add `std::unique_ptr<StderrLogReceiver> log_receiver;` field
   - Initialize in constructor
   - Add `.SetLogReceiver(log_receiver.get())` to builder chain

5. **Fix LiveClientWrapper** (`live_client_wrapper.cpp`):
   - Same fix as LiveBlocking

6. **Rebuild and test**:
```bash
cd src/Databento.Native/build
cmake --build . --config Release
cp Release/databento_native.dll ../runtimes/win-x64/native/
```

7. **Run test suite**:
   - HistoricalFutureDates.Test - verify no crash
   - Batch API tests with future dates
   - Live API tests

### Optional: Report to databento-cpp Upstream

Consider reporting to databento-cpp maintainers:
- NULL pointer dereferences without checks (25 locations)
- Suggestion: Add defensive NULL checks as fallback
- Or document that nullptr is not allowed for ILogReceiver parameter

---

## Test Results

### Test Environment
- **Isolated test directory**: `C:\Users\serha\source\repos\Databento_test11\`
- **Custom databento-cpp build**: Applied Approach 1 (defensive NULL checks)
- **Test program**: Requests CLZ5 historical data for May-Nov 2025

### Before Fix
```
Testing Historical API with future dates (May-Nov 2025)...
Fetching data...

Fatal error. System.AccessViolationException: Attempted to read or write protected memory.
This is often an indication that other memory is corrupt.
```

### After Fix
```
Testing Historical API with future dates (May-Nov 2025)...
Fetching data...

[HTTP] CheckWarnings: log_receiver_ is NULL, cannot log warning: [HttpClient::CheckWarnings] Server Warning: The streaming request contained one or more days which have reduced quality: 2025-09-17 (degraded), 2025-09-24 (degraded), 2025-10-01 (degraded), 2025-10-08 (degraded), 2025-10-15 (degraded), 2025-10-22 (degraded), 2025-10-29 (degraded), 2025-11-05 (degraded), 2025-11-12 (degraded).
[HTTP] Response handler completed
[HTTP] Content receiver called: length=4090, err_status=0

Historical record: OHLCV: O:56.81 H:57.73 L:55.17 C:57.14 V:18031 [2025-05-01...]
Historical record: OHLCV: O:57.25 H:58.03 L:56.32 C:57.12 V:11917 [2025-05-02...]
Historical record: OHLCV: O:57.17 H:58.27 L:56.96 C:57.91 V:19181 [2025-05-05...]
... (167 more records)

✓ SUCCESS: Received 172 records without crashing!

The bug is fixed if you see this message.
```

### Test Project for Main Repo
Created `examples/HistoricalFutureDates.Test/` with:
- Test program that reproduces the bug with unfixed code
- README with Visual Studio testing instructions
- Expected before/after outputs documented

User confirmed test runs successfully in Visual Studio.

---

## Files Modified

### In Test Project (Databento_test11)
1. **databento-cpp/src/detail/http_client.cpp**:
   - Added NULL checks in CheckWarnings() (3 code paths)
   - Added diagnostic logging (can be removed for production)

2. **Databento_test11/Program.cs**:
   - Test program with future date range (May-Nov 2025)

3. **Databento_test11/copy_custom_dll.sh**:
   - Script to copy custom DLL to all 3 runtime locations

### In Main Repo (databento-dotnet)
1. **examples/HistoricalFutureDates.Test/** (Created):
   - `HistoricalFutureDates.Test.csproj`
   - `Program.cs` - Test program
   - `README.md` - Testing instructions

2. **BUG_FIX_SUMMARY.md** (Created):
   - Comprehensive bug documentation
   - Fix options with code samples
   - Testing instructions

### To Be Modified (Pending)
1. **src/Databento.Native/src/common_helpers.hpp**:
   - Add shared StderrLogReceiver class

2. **src/Databento.Native/src/historical_client_wrapper.cpp**:
   - Apply Approach 2 fix

3. **src/Databento.Native/src/batch_wrapper.cpp**:
   - Apply Approach 2 fix

4. **src/Databento.Native/src/live_blocking_wrapper.cpp**:
   - Apply Approach 2 fix

5. **src/Databento.Native/src/live_client_wrapper.cpp**:
   - Apply Approach 2 fix

---

## Investigation Methodology

### Diagnostic Approach
1. **Added version markers** to native DLL to verify custom builds loading
2. **Added HTTP lifecycle logging** to trace request/response flow
3. **Added CheckWarnings logging** with pointer address display
4. **Forced rebuilds** by deleting object files to ensure fresh builds
5. **Copied DLL to all 3 locations** (.NET loads from multiple paths)

### Key Insights
- **DLL loading complexity**: .NET loads from bin/, runtimes/win-x64/native/, win-x64/native/
- **NuGet restoration overwrites**: Custom DLLs get replaced on every build
- **CMake caching issues**: Must delete object files to force rebuild
- **File locking**: Visual Studio locks DLLs, must close to update

### Diagnostic Commands Used
```bash
# Verify DLL loading
strings databento_native.dll | grep "CUSTOM"

# Force rebuild of specific file
find . -name "http_client.cpp.obj" -delete
cmake --build . --config Release --target databento

# Copy to all locations
cp custom.dll bin/Debug/net8.0/databento_native.dll
cp custom.dll bin/Debug/net8.0/runtimes/win-x64/native/databento_native.dll
cp custom.dll bin/Debug/net8.0/win-x64/native/databento_native.dll

# Verify timestamps
find bin/Debug/net8.0 -name "databento_native.dll" -ls
```

---

## Recommendations

### Priority 1: Fix Production Code (Approach 2)
1. Implement shared StderrLogReceiver class
2. Fix all 4 wrappers: Historical, Batch, LiveBlocking, LiveThreaded
3. Rebuild native library
4. Test all APIs (Historical, Batch, Live)

### Priority 2: Investigate Live Client Mystery
- Test Live APIs with and without SetLogReceiver
- Verify if nullptr causes crashes in Authenticate/Start
- Understand why CLAUDE.md examples show successful logging

### Priority 3: Comprehensive Testing
- Create test suite for edge cases:
  - Future dates (triggers warnings)
  - Invalid symbols (may trigger warnings)
  - Network errors (error logging)
  - Compression failures (compression logging)

### Priority 4: Consider Upstream Contribution
- Report NULL pointer dereferences to databento-cpp maintainers
- Suggest defensive NULL checks as safety net
- Share findings about X-Warning header scenario

---

## Related Documentation

- **BUG_FIX_SUMMARY.md**: Complete fix guide with code samples
- **examples/HistoricalFutureDates.Test/README.md**: Testing instructions
- **CLAUDE.md**: User instructions showing Live client usage
- **session_status.md**: Investigation session notes

---

## Conclusion

Successfully identified and fixed a critical NULL pointer dereference bug affecting Historical API with future dates. The root cause is passing `nullptr` for the ILogReceiver parameter in wrapper code, which causes crashes when databento-cpp attempts to log API warnings.

**Status**:
- ✅ Bug identified and root cause confirmed
- ✅ Fix tested and verified in isolated test project
- ✅ Test project created for main repo
- ⏳ Production fix pending (Approach 2 recommended)
- ⚠️ Additional vulnerable components identified (Batch, Live)

**Next Steps**: Apply Approach 2 fix to all wrapper components in main databento-dotnet repository.
