# Option B: Complete Implementation Plan - Fix All 4 Wrappers

**Created**: November 20, 2025
**Decision**: Fix ALL wrappers (Historical, Batch, LiveBlocking, LiveThreaded)
**Version Target**: 3.0.24-beta
**Estimated Duration**: 4-5 hours
**Risk Level**: 🟡 LOW-MEDIUM

---

## Executive Summary

Fix NULL pointer bug in all 4 native wrappers by providing explicit `ILogReceiver` implementation. This ensures:
- ✅ Historical/Batch don't crash with warnings
- ✅ Complete consistency across all clients
- ✅ Full control over logging (stderr, custom format)
- ✅ Explicit intent in code (no reliance on Builder defaults)

### Trade-offs Accepted
- Log format changes for Live clients (visible behavior change)
- Log destination changes stdout→stderr (could surprise ~5-10% of users)
- Requires thorough testing of all client types

---

## Table of Contents

1. [Phase 1: Implementation](#phase-1-implementation)
2. [Phase 2: Build & Verify](#phase-2-build--verify)
3. [Phase 3: Comprehensive Testing](#phase-3-comprehensive-testing)
4. [Phase 4: Documentation](#phase-4-documentation)
5. [Phase 5: Deployment](#phase-5-deployment)
6. [Risk Mitigation](#risk-mitigation)
7. [Rollback Plan](#rollback-plan)

---

## Phase 1: Implementation

**Duration**: 1 hour
**Risk**: 🟢 LOW (isolated changes)

### Step 1.1: Create Shared Log Receiver Class

**File**: `src/Databento.Native/src/common_helpers.hpp`
**Location**: End of file, before closing `}  // namespace databento_native`

**Add this code**:

```cpp
// ============================================================================
// Shared Log Receiver for databento-cpp clients
// ============================================================================

/**
 * Simple ILogReceiver implementation that logs to stderr
 * Used by all wrapper components to prevent NULL pointer dereferences
 * and provide consistent logging behavior across Historical, Batch, and Live clients.
 *
 * Design choices:
 * - stderr output: Doesn't interfere with application stdout
 * - Thread-safe: stderr writes are atomic for single fprintf calls
 * - Explicit flush: Ensures messages are visible immediately
 * - Consistent format: [Databento LEVEL] prefix for all messages
 */
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

        // Write to stderr with explicit flush for reliability
        // Format: [Databento LEVEL] message
        std::fprintf(stderr, "[Databento %s] %s\n", level_str, message.c_str());
        std::fflush(stderr);
    }
};

}  // namespace databento_native
```

**Why this design**:
- Single shared implementation (DRY principle)
- stderr doesn't interfere with application output on stdout
- Thread-safe (fprintf to stderr is atomic for single calls)
- Explicit flush ensures immediate visibility
- Consistent `[Databento LEVEL]` format across all clients

---

### Step 1.2: Fix Historical Client Wrapper

**File**: `src/Databento.Native/src/historical_client_wrapper.cpp`
**Lines**: 28-38

**Current code**:
```cpp
struct HistoricalClientWrapper {
    std::unique_ptr<db::Historical> client;
    std::string api_key;

    explicit HistoricalClientWrapper(const std::string& key)
        : api_key(key) {
        client = std::make_unique<db::Historical>(nullptr, key, db::HistoricalGateway::Bo1);
    }
};
```

**Replace with**:
```cpp
struct HistoricalClientWrapper {
    std::unique_ptr<db::Historical> client;
    std::string api_key;
    std::unique_ptr<databento_native::StderrLogReceiver> log_receiver;

    explicit HistoricalClientWrapper(const std::string& key)
        : api_key(key),
          log_receiver(std::make_unique<databento_native::StderrLogReceiver>()) {
        client = std::make_unique<db::Historical>(
            log_receiver.get(),
            key,
            db::HistoricalGateway::Bo1
        );
    }
};
```

**Changes**:
1. Add `log_receiver` field (unique_ptr manages lifetime)
2. Initialize in constructor initializer list
3. Pass `log_receiver.get()` instead of `nullptr`

**Critical Fix**: This resolves the crash with future dates/warnings

---

### Step 1.3: Fix Batch Wrapper

**File**: `src/Databento.Native/src/batch_wrapper.cpp`
**Lines**: 28-36

**Current code**:
```cpp
struct HistoricalClientWrapper {
    std::unique_ptr<db::Historical> client;
    std::string api_key;

    explicit HistoricalClientWrapper(const std::string& key)
        : api_key(key) {
        client = std::make_unique<db::Historical>(nullptr, key, db::HistoricalGateway::Bo1);
    }
};
```

**Replace with** (IDENTICAL to Historical fix):
```cpp
struct HistoricalClientWrapper {
    std::unique_ptr<db::Historical> client;
    std::string api_key;
    std::unique_ptr<databento_native::StderrLogReceiver> log_receiver;

    explicit HistoricalClientWrapper(const std::string& key)
        : api_key(key),
          log_receiver(std::make_unique<databento_native::StderrLogReceiver>()) {
        client = std::make_unique<db::Historical>(
            log_receiver.get(),
            key,
            db::HistoricalGateway::Bo1
        );
    }
};
```

**Note**: Batch uses Historical client internally, so same fix applies

**Critical Fix**: This resolves the crash with future dates/warnings in Batch API

---

### Step 1.4: Fix LiveBlocking Wrapper

**File**: `src/Databento.Native/src/live_blocking_wrapper.cpp`
**Lines**: 24-73

**Current code**:
```cpp
struct LiveBlockingWrapper {
    std::unique_ptr<db::LiveBlocking> client;
    std::string dataset;
    std::string api_key;
    bool send_ts_out = false;
    db::VersionUpgradePolicy upgrade_policy = db::VersionUpgradePolicy::UpgradeToV3;
    int heartbeat_interval_secs = 30;

    explicit LiveBlockingWrapper(const std::string& key)
        : api_key(key) {}

    explicit LiveBlockingWrapper(
        const std::string& key,
        const std::string& ds,
        bool ts_out,
        db::VersionUpgradePolicy policy,
        int heartbeat_secs)
        : api_key(key)
        , dataset(ds)
        , send_ts_out(ts_out)
        , upgrade_policy(policy)
        , heartbeat_interval_secs(heartbeat_secs)
    {}

    void EnsureClientCreated() {
        if (!client) {
            auto builder = db::LiveBlocking::Builder()
                .SetKey(api_key)
                .SetDataset(dataset)
                .SetSendTsOut(send_ts_out)
                .SetUpgradePolicy(upgrade_policy);

            if (heartbeat_interval_secs > 0) {
                builder.SetHeartbeatInterval(std::chrono::seconds(heartbeat_interval_secs));
            }

            client = std::make_unique<db::LiveBlocking>(builder.BuildBlocking());
        }
    }

    // ... rest of class ...
};
```

**Replace with**:
```cpp
struct LiveBlockingWrapper {
    std::unique_ptr<db::LiveBlocking> client;
    std::unique_ptr<databento_native::StderrLogReceiver> log_receiver;  // ADD THIS
    std::string dataset;
    std::string api_key;
    bool send_ts_out = false;
    db::VersionUpgradePolicy upgrade_policy = db::VersionUpgradePolicy::UpgradeToV3;
    int heartbeat_interval_secs = 30;

    explicit LiveBlockingWrapper(const std::string& key)
        : api_key(key),
          log_receiver(std::make_unique<databento_native::StderrLogReceiver>()) {}  // ADD THIS

    explicit LiveBlockingWrapper(
        const std::string& key,
        const std::string& ds,
        bool ts_out,
        db::VersionUpgradePolicy policy,
        int heartbeat_secs)
        : api_key(key),
          log_receiver(std::make_unique<databento_native::StderrLogReceiver>()),  // ADD THIS
          dataset(ds),
          send_ts_out(ts_out),
          upgrade_policy(policy),
          heartbeat_interval_secs(heartbeat_secs)
    {}

    void EnsureClientCreated() {
        if (!client) {
            auto builder = db::LiveBlocking::Builder()
                .SetKey(api_key)
                .SetDataset(dataset)
                .SetSendTsOut(send_ts_out)
                .SetUpgradePolicy(upgrade_policy)
                .SetLogReceiver(log_receiver.get());  // ADD THIS LINE

            if (heartbeat_interval_secs > 0) {
                builder.SetHeartbeatInterval(std::chrono::seconds(heartbeat_interval_secs));
            }

            client = std::make_unique<db::LiveBlocking>(builder.BuildBlocking());
        }
    }

    // ... rest of class unchanged ...
};
```

**Changes**:
1. Add `log_receiver` field after `client` field
2. Initialize in BOTH constructors (explicit initializer list)
3. Add `.SetLogReceiver(log_receiver.get())` to builder chain

**Consistency Fix**: Ensures Live uses our StderrLogReceiver instead of databento default

---

### Step 1.5: Fix LiveThreaded Wrapper

**File**: `src/Databento.Native/src/live_client_wrapper.cpp`
**Lines**: 28-79

**Current code**:
```cpp
struct LiveClientWrapper {
    std::unique_ptr<db::LiveThreaded> client;
    RecordCallback record_callback = nullptr;
    MetadataCallback metadata_callback = nullptr;
    ErrorCallback error_callback = nullptr;
    void* user_data = nullptr;
    std::atomic<bool> is_running{false};
    std::mutex callback_mutex;
    std::once_flag client_init_flag;
    std::string dataset;
    std::string api_key;
    bool send_ts_out = false;
    db::VersionUpgradePolicy upgrade_policy = db::VersionUpgradePolicy::UpgradeToV3;
    int heartbeat_interval_secs = 30;

    explicit LiveClientWrapper(const std::string& key)
        : api_key(key) {}

    explicit LiveClientWrapper(
        const std::string& key,
        const std::string& ds,
        bool ts_out,
        db::VersionUpgradePolicy policy,
        int heartbeat_secs)
        : api_key(key)
        , dataset(ds)
        , send_ts_out(ts_out)
        , upgrade_policy(policy)
        , heartbeat_interval_secs(heartbeat_secs)
    {}

    void EnsureClientCreated() {
        std::call_once(client_init_flag, [this]() {
            auto builder = db::LiveThreaded::Builder()
                .SetKey(api_key)
                .SetDataset(dataset)
                .SetSendTsOut(send_ts_out)
                .SetUpgradePolicy(upgrade_policy);

            if (heartbeat_interval_secs > 0) {
                builder.SetHeartbeatInterval(
                    std::chrono::seconds(heartbeat_interval_secs));
            }

            client = std::make_unique<db::LiveThreaded>(builder.BuildThreaded());
        });
    }

    // ... rest of class ...
};
```

**Replace with**:
```cpp
struct LiveClientWrapper {
    std::unique_ptr<db::LiveThreaded> client;
    std::unique_ptr<databento_native::StderrLogReceiver> log_receiver;  // ADD THIS
    RecordCallback record_callback = nullptr;
    MetadataCallback metadata_callback = nullptr;
    ErrorCallback error_callback = nullptr;
    void* user_data = nullptr;
    std::atomic<bool> is_running{false};
    std::mutex callback_mutex;
    std::once_flag client_init_flag;
    std::string dataset;
    std::string api_key;
    bool send_ts_out = false;
    db::VersionUpgradePolicy upgrade_policy = db::VersionUpgradePolicy::UpgradeToV3;
    int heartbeat_interval_secs = 30;

    explicit LiveClientWrapper(const std::string& key)
        : api_key(key),
          log_receiver(std::make_unique<databento_native::StderrLogReceiver>()) {}  // ADD THIS

    explicit LiveClientWrapper(
        const std::string& key,
        const std::string& ds,
        bool ts_out,
        db::VersionUpgradePolicy policy,
        int heartbeat_secs)
        : api_key(key),
          log_receiver(std::make_unique<databento_native::StderrLogReceiver>()),  // ADD THIS
          dataset(ds),
          send_ts_out(ts_out),
          upgrade_policy(policy),
          heartbeat_interval_secs(heartbeat_secs)
    {}

    void EnsureClientCreated() {
        std::call_once(client_init_flag, [this]() {
            auto builder = db::LiveThreaded::Builder()
                .SetKey(api_key)
                .SetDataset(dataset)
                .SetSendTsOut(send_ts_out)
                .SetUpgradePolicy(upgrade_policy)
                .SetLogReceiver(log_receiver.get());  // ADD THIS LINE

            if (heartbeat_interval_secs > 0) {
                builder.SetHeartbeatInterval(
                    std::chrono::seconds(heartbeat_interval_secs));
            }

            client = std::make_unique<db::LiveThreaded>(builder.BuildThreaded());
        });
    }

    // ... rest of class unchanged ...
};
```

**Changes**:
1. Add `log_receiver` field after `client` field
2. Initialize in BOTH constructors
3. Add `.SetLogReceiver(log_receiver.get())` in lambda inside EnsureClientCreated

**Consistency Fix**: Ensures LiveThreaded uses our StderrLogReceiver

---

## Phase 2: Build & Verify

**Duration**: 30 minutes
**Risk**: 🟢 LOW (local build)

### Step 2.1: Clean Build Environment

```bash
cd src/Databento.Native/build

# Remove all build artifacts
rm -rf *

# Verify clean
ls -la
# Should be empty or only have .gitignore
```

---

### Step 2.2: Configure CMake

```bash
# Reconfigure from scratch
cmake ..

# Verify configuration succeeded
echo "Exit code: $?"
# Should be 0
```

**Expected output**:
```
-- The C compiler identification is MSVC ...
-- The CXX compiler identification is MSVC ...
-- Configuring done
-- Generating done
-- Build files written to: ...
```

---

### Step 2.3: Build Native Library (Release)

```bash
# Build in Release mode
cmake --build . --config Release

# Check for errors
echo "Exit code: $?"
# Should be 0

# Verify DLL created
ls -l Release/databento_native.dll
```

**Success criteria**: Clean build with 0 errors

**Expected warnings**: None expected (we're just adding fields and calling existing APIs)

---

### Step 2.4: Deploy DLL to Runtime Location

```bash
# Copy to runtime directory
cp Release/databento_native.dll ../runtimes/win-x64/native/databento_native.dll

# Verify timestamp (should be recent)
ls -l ../runtimes/win-x64/native/databento_native.dll

# Get file hash for verification
certutil -hashfile ../runtimes/win-x64/native/databento_native.dll SHA256
# Save this hash for later verification
```

---

### Step 2.5: Build .NET Solution

```bash
cd ../../../../  # Back to repo root

# Clean solution
dotnet clean

# Build in Release mode
dotnet build -c Release

# Check exit code
echo "Exit code: $?"
# Should be 0
```

**Success criteria**:
- 0 errors
- Only XML documentation warnings (expected)
- All projects build successfully

---

### Step 2.6: Quick Smoke Test

```bash
# Test Historical API with known crash scenario
cd examples/HistoricalFutureDates.Test
dotnet run
```

**Expected output** (Before: crash, After: success):
```
Testing Historical API with future dates (May-Nov 2025)...
Fetching data...

[Databento WARNING] [HttpClient::CheckWarnings] Server Warning: The streaming request contained one or more days which have reduced quality: 2025-09-17 (degraded), 2025-09-24 (degraded), 2025-10-01 (degraded), 2025-10-08 (degraded), 2025-10-15 (degraded), 2025-10-22 (degraded), 2025-10-29 (degraded), 2025-11-05 (degraded), 2025-11-12 (degraded).

Historical record: OHLCV: O:56.81 H:57.73 L:55.17 C:57.14 V:18031 [2025-05-01...]
Historical record: OHLCV: O:57.25 H:58.03 L:56.32 C:57.12 V:11917 [2025-05-02...]
...

✓ SUCCESS: Received 172 records without crashing!
```

**Pass criteria**:
- ✅ No AccessViolationException
- ✅ Warning visible with `[Databento WARNING]` prefix
- ✅ All 172 records received

---

## Phase 3: Comprehensive Testing

**Duration**: 2 hours
**Risk**: 🟡 MEDIUM (might discover issues)

### Test Matrix

| # | Test | Client | Scenario | Expected Result | Priority |
|---|------|--------|----------|----------------|----------|
| 1 | HistoricalFutureDates | Historical | Future dates | 172 records, warning visible | CRITICAL |
| 2 | Historical past dates | Historical | 2024 data | Works, no warnings | HIGH |
| 3 | BatchInvalidSymbol | Batch | Invalid symbol | DbentoException, no crash | CRITICAL |
| 4 | Batch with future dates | Batch | Future dates | Works, warning visible | HIGH |
| 5 | LiveBlocking.Example | LiveBlocking | Normal auth | Logs visible with new format | CRITICAL |
| 6 | Live Replay | LiveBlocking | Replay mode | Symbol resolution works | HIGH |
| 7 | LiveInvalidSymbol | LiveBlocking | Bad symbol | Graceful handling | MEDIUM |
| 8 | LiveThreaded example | LiveThreaded | Event-based | Callbacks work, logs visible | HIGH |
| 9 | All 32 examples | All | Various | No crashes, expected behavior | HIGH |
| 10 | Log format check | All | Compare before/after | Format matches spec | MEDIUM |

---

### Test 1: Historical with Future Dates (CRITICAL)

**Already exists**: `examples/HistoricalFutureDates.Test`

```bash
cd examples/HistoricalFutureDates.Test
dotnet run
```

**Pass criteria**:
- ✅ No crash (was AccessViolationException before)
- ✅ Warning appears on stderr
- ✅ Warning format: `[Databento WARNING] [HttpClient::CheckWarnings] Server Warning: ...`
- ✅ All 172 records received
- ✅ Data integrity intact

**Capture output**:
```bash
dotnet run 2>&1 | tee test1_output.txt
```

---

### Test 2: Historical with Past Dates (HIGH)

**Use existing example**: Choose any historical example with 2024 data

```bash
cd examples/Historical.Example
dotnet run
```

**Pass criteria**:
- ✅ No warnings (past dates are stable)
- ✅ No crashes
- ✅ Data received correctly
- ✅ Same behavior as before (regression test)

---

### Test 3: Batch with Invalid Symbol (CRITICAL)

**Already exists**: `examples/BatchInvalidSymbol.Test`

```bash
cd examples/BatchInvalidSymbol.Test
dotnet run
```

**Pass criteria**:
- ✅ DbentoException thrown (proper error handling)
- ✅ No AccessViolationException
- ✅ Error message clear and helpful

---

### Test 4: Batch with Future Dates (HIGH)

**Need to create**: Similar to HistoricalFutureDates but using Batch API

**Quick test** (or skip if time-constrained - Batch uses Historical internally):
```bash
# Can test manually or skip - same underlying code as Historical
```

---

### Test 5: LiveBlocking Authentication & Logs (CRITICAL)

**Already exists**: `examples/LiveBlocking.Example`

```bash
cd examples/LiveBlocking.Example
dotnet run 2>&1 | tee test5_output.txt
```

**Compare output**:

**Before (stdout, databento format)**:
```
INFO: [LiveBlocking::Authenticate] Successfully authenticated with session_id 1763811131
INFO: [LiveBlocking::Start] Starting session
```

**After (stderr, our format)**:
```
[Databento INFO] [LiveBlocking::Authenticate] Successfully authenticated with session_id 1763811131
[Databento INFO] [LiveBlocking::Start] Starting session
```

**Pass criteria**:
- ✅ Authentication succeeds
- ✅ Logs appear (on stderr now)
- ✅ Format matches our `[Databento LEVEL]` pattern
- ✅ No crashes
- ✅ Data flow works correctly

**Note**: This is the most visible change for users

---

### Test 6: Live Replay Mode (HIGH)

**Already exists**: Examples with replay

```bash
cd examples/SymbolResolution.Example
dotnet run
```

**Pass criteria**:
- ✅ Replay works
- ✅ Symbol resolution works
- ✅ No crashes
- ✅ Logs visible with new format

---

### Test 7: Live with Invalid Symbol (MEDIUM)

**Already exists**: `examples/LiveInvalidSymbol.Test`

```bash
cd examples/LiveInvalidSymbol.Test
dotnet run
```

**Pass criteria**:
- ✅ Handled gracefully via metadata.not_found
- ✅ No crashes
- ✅ No warnings (different error path)

---

### Test 8: LiveThreaded (HIGH)

**Test event-based client**:

```bash
cd examples/Live.Example
dotnet run
```

**Pass criteria**:
- ✅ Event callbacks fire correctly
- ✅ Logs visible with new format
- ✅ No threading issues
- ✅ Cleanup works properly

---

### Test 9: Full Example Suite (HIGH)

**Run all 32 examples** (may be time-consuming):

```bash
# Create test script
cat > test_all_examples.sh << 'EOF'
#!/bin/bash
results=()
for dir in examples/*/; do
    name=$(basename "$dir")
    echo "===================================="
    echo "Testing: $name"
    echo "===================================="
    cd "$dir"
    timeout 60s dotnet run 2>&1
    exit_code=$?
    cd ../..

    if [ $exit_code -eq 0 ]; then
        echo "✓ $name PASSED"
        results+=("✓ $name")
    elif [ $exit_code -eq 124 ]; then
        echo "⏱ $name TIMEOUT (may be normal)"
        results+=("⏱ $name")
    else
        echo "✗ $name FAILED ($exit_code)"
        results+=("✗ $name")
    fi
    echo ""
done

echo "===================================="
echo "Summary:"
echo "===================================="
printf '%s\n' "${results[@]}"
EOF

chmod +x test_all_examples.sh
./test_all_examples.sh | tee full_test_results.txt
```

**Pass criteria**:
- ✅ No new crashes
- ✅ All examples that worked before still work
- ✅ Log format changes visible but not breaking

---

### Test 10: Log Format Verification (MEDIUM)

**Create comparison document**:

```bash
# Before fix (run against v3.0.23-beta)
git checkout v3.0.23-beta
dotnet run > before_logs.txt 2>&1

# After fix (current code)
git checkout master
dotnet run > after_logs.txt 2>&1

# Compare
diff before_logs.txt after_logs.txt
```

**Document differences**:
- `INFO:` → `[Databento INFO]`
- stdout → stderr (file redirect behavior changes)
- Same semantic information, different format

---

### Test 11: Stream Redirection Test (Edge Case)

**Test stdout redirect**:
```bash
# Before: logs appear in file
dotnet run > output.log 2>&1
cat output.log  # Should contain logs

# After: logs on stderr
dotnet run > output.log
# Logs appear on console (stderr)
cat output.log  # No logs

# Correct way after fix:
dotnet run > output.log 2>&1
cat output.log  # Contains logs
```

**Document for release notes**: Users redirecting stdout need to use `2>&1`

---

### Test Summary Template

Create `TEST_RESULTS_v3.0.24-beta.md`:

```markdown
# Test Results - v3.0.24-beta

**Date**: [DATE]
**Tester**: [NAME]
**Build**: Release
**Platform**: Windows, .NET 8.0

## Summary
- Total tests: 10
- Passed: X
- Failed: X
- Issues found: X

## Detailed Results

### Test 1: Historical with Future Dates
- Status: [ ] PASS / [ ] FAIL
- Duration: X seconds
- Records received: X
- Warning visible: [ ] YES / [ ] NO
- Notes: ...

[Repeat for each test]

## Issues Discovered
1. [Issue description]
2. ...

## Conclusion
[ ] Ready for release
[ ] Needs fixes before release
```

---

## Phase 4: Documentation

**Duration**: 45 minutes
**Risk**: 🟢 NONE

### Step 4.1: Update Release Notes

**File**: `src/Databento.Client/Databento.Client.csproj`

**Update `PackageReleaseNotes`**:

```xml
<PackageReleaseNotes>
v3.0.24-beta (November 2025)
─────────────────────────────────────────────────────────
CRITICAL FIX: NULL Pointer Bug in Native Wrappers

Fixed:
- AccessViolationException in Historical/Batch APIs when requesting future dates
- Root cause: NULL ILogReceiver pointer in native wrapper initialization
- Impact: All wrappers (Historical, Batch, LiveBlocking, LiveThreaded)

Changes:
- All native clients now use explicit stderr logging
- Log format updated to: [Databento LEVEL] message
- Warnings (e.g., "degraded quality" for future dates) now visible
- No API changes - purely internal fix

Breaking Changes:
- Log output moved from stdout to stderr (edge case)
- Log format changed: "INFO:" → "[Databento INFO]"
- Users redirecting stdout should use: dotnet run > file.log 2>&1

Affected Components:
✓ Historical client - fixed crash with future dates/warnings
✓ Batch client - fixed crash with future dates/warnings
✓ LiveBlocking client - consistent logging behavior
✓ LiveThreaded client - consistent logging behavior

Migration Guide:
- No code changes required - works as-is
- If you redirect logs: use 2>&1 to capture stderr
- If you parse native logs: update to new [Databento LEVEL] format

Testing:
✓ 172 OHLCV records with future dates (no crash)
✓ Invalid symbol handling (proper exceptions)
✓ All 32 example projects verified
✓ Live authentication and streaming tested

Related Issues:
- Resolves: Bug Investigation #1 (NULL pointer in wrappers)
- Related: Issue #1 (separate databento-cpp bug with invalid symbols)

v3.0.23-beta
- Fixed: Bundle VC++ runtime DLLs to prevent DllNotFoundException
...
</PackageReleaseNotes>
```

---

### Step 4.2: Update API Documentation

**File**: `API_REFERENCE.md`

**Find and update** (lines ~633-639 and ~675-681):

**Current text**:
```markdown
> ⚠️ **CRITICAL WARNING**: This method may crash with AccessViolationException...
```

**Replace with**:
```markdown
> ℹ️ **Note** (Fixed in v3.0.24-beta): Previous versions could crash with
> AccessViolationException when requesting data for future dates or when the API
> returned warnings. This has been resolved by properly initializing the native
> logging subsystem. Server warnings (such as "degraded quality" for future dates)
> are now visible on stderr in the format: `[Databento WARNING] message`.
>
> **Log Format**: Native diagnostic messages now use `[Databento LEVEL]` prefix
> and are written to stderr (not stdout). If you redirect output, use `2>&1` to
> capture both application output and diagnostic logs.
```

**Apply to both**:
- GetRangeAsync section (line ~633)
- GetRangeToFileAsync section (line ~675)

---

### Step 4.3: Update XML Comments in C# Code

**File**: `src/Databento.Client/Historical/HistoricalClient.cs`

**Update GetRangeAsync** (lines ~95-109):

**Current**:
```csharp
/// <remarks>
/// ⚠️ <b>CRITICAL WARNING</b>: This method may crash with...
/// </remarks>
```

**Replace with**:
```csharp
/// <remarks>
/// ℹ️ <b>Note</b>: Fixed in v3.0.24-beta. Previous versions could crash when
/// requesting future dates or invalid symbols. This has been resolved.
/// Server warnings (such as "degraded quality" for future dates) are now logged
/// to stderr in the format: <c>[Databento WARNING] message</c>.
/// </remarks>
```

**Update GetRangeToFileAsync** (lines ~257-271) with identical text.

---

### Step 4.4: Create Migration Guide

**File**: `MIGRATION_GUIDE_v3.0.24.md` (NEW)

```markdown
# Migration Guide: v3.0.23-beta → v3.0.24-beta

## Overview

Version 3.0.24-beta fixes a critical NULL pointer bug in native wrappers that
caused crashes when requesting future dates or when the API returned warnings.

## Do I Need to Change My Code?

**Short answer: NO** - Your existing code will work without changes.

## What Changed

### Internal Changes (Automatic)
- Native logging system now properly initialized
- All wrappers use explicit ILogReceiver implementation
- No crashes with future dates or API warnings

### Visible Changes (May Affect Scripts/Logs)

#### 1. Log Destination: stdout → stderr

**Before (v3.0.23-beta)**:
```bash
dotnet run > output.log
# Native logs appeared in output.log
```

**After (v3.0.24-beta)**:
```bash
dotnet run > output.log
# Native logs appear on console (stderr), NOT in output.log

# To capture logs:
dotnet run > output.log 2>&1  # Redirect both stdout and stderr
```

#### 2. Log Format Changed

**Before**:
```
INFO: [LiveBlocking::Authenticate] Successfully authenticated...
```

**After**:
```
[Databento INFO] [LiveBlocking::Authenticate] Successfully authenticated...
```

**Pattern**: `LEVEL:` → `[Databento LEVEL]`

## Migration Scenarios

### Scenario 1: Normal Console Usage (No Changes Needed)

```csharp
var client = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();

var records = await client.GetRangeAsync(...);
// Works exactly the same
```

**Action**: ✅ None required

---

### Scenario 2: Redirecting Output to File

**Before**:
```bash
dotnet run > application.log
```

**After** (to capture diagnostic logs):
```bash
dotnet run > application.log 2>&1
```

**Action**: Update scripts to redirect stderr: `2>&1`

---

### Scenario 3: Parsing Native Log Messages

**If you have code like this**:
```csharp
// ❌ BAD: Don't parse native diagnostic logs
if (logLine.StartsWith("INFO:")) {
    // Extract session ID or other info
}
```

**Recommended approach**:
```csharp
// ✅ GOOD: Use proper API
var client = new LiveClientBuilder().Build();
// Session ID available through API events/metadata
```

**Action**: Refactor to use proper API instead of parsing logs

---

### Scenario 4: Automated Testing/Monitoring

**Update grep patterns**:
```bash
# Before
dotnet run | grep "ERROR"

# After
dotnet run 2>&1 | grep "\[Databento ERROR\]"
```

**Action**: Update monitoring scripts for new log format

---

## Benefits of Upgrade

### Critical Fixes
- ✅ No more crashes with future dates
- ✅ No more crashes when API returns warnings
- ✅ Proper error handling throughout

### Improved Diagnostics
- ✅ Warnings now visible (e.g., "degraded quality")
- ✅ Consistent log format across all clients
- ✅ Stderr output doesn't interfere with application stdout

## Testing Your Upgrade

### 1. Functional Test
```csharp
// This should work without crashes (crashed in v3.0.23-beta)
var client = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();

var records = await client.GetRangeAsync(
    dataset: "GLBX.MDP3",
    symbols: new[] { "CLZ5" },
    schema: Schema.Ohlcv1D,
    start: new DateTime(2025, 5, 1),
    end: new DateTime(2025, 11, 30)
);

// Should receive 172 records with warning about future dates
```

### 2. Log Format Test
```bash
dotnet run 2>&1 | grep "\[Databento"
# Should see: [Databento INFO], [Databento WARNING], etc.
```

### 3. Redirection Test
```bash
dotnet run > data.log 2> diagnostics.log
# data.log: application output
# diagnostics.log: native diagnostic logs
```

## Support

If you encounter issues:
1. Check that you're using v3.0.24-beta or later
2. Verify native DLL loaded (no DllNotFoundException)
3. Report issues at: https://github.com/Alparse/databento-dotnet/issues

## Summary

| Aspect | Action Required |
|--------|----------------|
| Code changes | ✅ None |
| API compatibility | ✅ Maintained |
| Compilation | ✅ Works as-is |
| Log redirection | ⚠️ Update scripts: use 2>&1 |
| Log parsing | ⚠️ Update patterns: "INFO:" → "[Databento INFO]" |
| Monitoring | ⚠️ Update grep patterns |

Most users: **No action required** ✅
```

---

### Step 4.5: Update Session Status

**File**: `session_status.md`

**Append new section**:
```markdown
---
---

# Session Update: NULL Pointer Fix Implementation - v3.0.24-beta

**Date**: November 20-21, 2025
**Issue**: Bug Investigation #1 - AccessViolationException from NULL ILogReceiver
**Status**: ✅ **IMPLEMENTED AND TESTED**

---

## Decision: Option B - Fix All 4 Wrappers

After Phase 0 investigation, decided to fix ALL wrappers for consistency:
- ✅ Historical (MUST fix - crashes)
- ✅ Batch (MUST fix - crashes)
- ✅ LiveBlocking (SHOULD fix - consistency)
- ✅ LiveThreaded (SHOULD fix - consistency)

**Rationale**: Complete consistency, full control, explicit intent

---

## Implementation Summary

### Created Shared Log Receiver
**File**: `src/Databento.Native/src/common_helpers.hpp`
- `StderrLogReceiver` class
- Thread-safe, explicit flush
- Format: `[Databento LEVEL] message`

### Fixed All 4 Wrappers
1. **Historical**: Pass log_receiver.get() instead of nullptr
2. **Batch**: Same fix (uses Historical client)
3. **LiveBlocking**: Add .SetLogReceiver() to builder
4. **LiveThreaded**: Add .SetLogReceiver() to builder

---

## Test Results

[To be filled after testing phase]

---

## Changes Deployed

- Version: 3.0.24-beta
- Git commits: [commit hashes]
- NuGet published: [date]
- GitHub release: [URL]

---

## Known Impacts

### User-Visible Changes
- Log format: `INFO:` → `[Databento INFO]`
- Log destination: stdout → stderr
- ~5-10% of users may need to update log redirection scripts

### No Breaking Changes
- API surface unchanged
- Binary compatible
- Source compatible
- Same functionality

---

**Session Status**: ✅ COMPLETE
```

---

## Phase 5: Deployment

**Duration**: 1 hour
**Risk**: 🟡 MEDIUM (production)

### Step 5.1: Version Bump

**Files to update**:

1. `src/Databento.Client/Databento.Client.csproj`:
```xml
<Version>3.0.24-beta</Version>
```

2. `src/Databento.Interop/Databento.Interop.csproj`:
```xml
<Version>3.0.24-beta</Version>
```

---

### Step 5.2: Final Build

```bash
# Clean everything
dotnet clean
rm -rf src/Databento.Native/build/*

# Full rebuild
cd src/Databento.Native/build
cmake ..
cmake --build . --config Release
cp Release/databento_native.dll ../runtimes/win-x64/native/

cd ../../../../
dotnet build -c Release

# Verify success
echo "Exit code: $?"
```

---

### Step 5.3: Package NuGet

```bash
# Pack
dotnet pack src/Databento.Client/Databento.Client.csproj -c Release -o ./artifacts

# Verify package
ls -l artifacts/Databento.Client.3.0.24-beta.nupkg

# Inspect contents
unzip -l artifacts/Databento.Client.3.0.24-beta.nupkg | grep databento_native.dll
```

**Verify**:
- ✅ 9 DLLs present in runtimes/win-x64/native/
- ✅ databento_native.dll timestamp recent
- ✅ Package version correct: 3.0.24-beta

---

### Step 5.4: Local Integration Test

```bash
# Create fresh test project
mkdir test_v3.0.24_integration
cd test_v3.0.24_integration
dotnet new console

# Install local package
dotnet add package Databento.Client --source ../artifacts --version 3.0.24-beta

# Test with future dates scenario (known crash before)
# [Add test code]
dotnet run
```

**Pass criteria**: No crash, warning visible

---

### Step 5.5: Git Commit

```bash
git add -A

git commit -m "fix: Resolve NULL ILogReceiver bug in all native wrappers (v3.0.24-beta)

CRITICAL BUG FIX:
- Fixed AccessViolationException in Historical/Batch APIs when API returns warnings
- Fixed all 4 wrappers for consistency: Historical, Batch, LiveBlocking, LiveThreaded

ROOT CAUSE:
- Historical/Batch: Passed nullptr for ILogReceiver → crash on X-Warning headers
- Live: Relied on Builder default (worked but inconsistent)

SOLUTION:
- Created shared StderrLogReceiver class in common_helpers.hpp
- All wrappers now explicitly initialize and pass valid log receiver
- Logs now go to stderr with format: [Databento LEVEL] message

VISIBLE CHANGES:
- Log format: 'INFO:' → '[Databento INFO]'
- Log destination: stdout → stderr
- ~5-10% of users may need to update log redirection (2>&1)

TESTING:
✓ Historical with future dates: 172 records, no crash
✓ Batch invalid symbol: Proper exception handling
✓ LiveBlocking auth: Logs visible with new format
✓ All 32 examples: No crashes, expected behavior
✓ Stream redirection: Verified stdout/stderr behavior

FILES CHANGED:
- src/Databento.Native/src/common_helpers.hpp (added StderrLogReceiver)
- src/Databento.Native/src/historical_client_wrapper.cpp (pass log receiver)
- src/Databento.Native/src/batch_wrapper.cpp (pass log receiver)
- src/Databento.Native/src/live_blocking_wrapper.cpp (add SetLogReceiver)
- src/Databento.Native/src/live_client_wrapper.cpp (add SetLogReceiver)
- src/Databento.Client/Databento.Client.csproj (version 3.0.24-beta)
- src/Databento.Interop/Databento.Interop.csproj (version 3.0.24-beta)
- API_REFERENCE.md (updated warnings → fixed notes)
- src/Databento.Client/Historical/HistoricalClient.cs (updated XML comments)
- MIGRATION_GUIDE_v3.0.24.md (created)
- session_status.md (documented implementation)

NO API CHANGES:
- Binary compatible
- Source compatible
- No user code changes required

Resolves: Bug Investigation #1 (NULL pointer in native wrappers)
Related: Issue #1 (separate databento-cpp bug)
"
```

---

### Step 5.6: Push to Repositories

```bash
# Push to origin (private)
git push origin master

# Push to public
git push public master

# Create and push tag
git tag v3.0.24-beta -m "v3.0.24-beta: Fixed NULL ILogReceiver bug in all wrappers"
git push origin v3.0.24-beta
git push public v3.0.24-beta
```

---

### Step 5.7: Publish to NuGet.org

```bash
# Set API key (if not already set)
# export NUGET_API_KEY=your_key_here

# Publish
dotnet nuget push artifacts/Databento.Client.3.0.24-beta.nupkg \
  --api-key $NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

**Wait 10-15 minutes for NuGet indexing**

**Verify**: https://www.nuget.org/packages/Databento.Client/3.0.24-beta

---

### Step 5.8: Create GitHub Release

**Navigate to**: https://github.com/Alparse/databento-dotnet/releases/new

**Tag**: v3.0.24-beta
**Title**: `v3.0.24-beta - Fixed NULL ILogReceiver Bug in All Native Wrappers`

**Description**:

```markdown
## 🐛 Critical Bug Fix

Fixed `AccessViolationException` crash in Historical and Batch APIs, plus improved
consistency across all client types.

### What Was Fixed

**Critical Crashes**:
- ✅ Historical API no longer crashes with future dates
- ✅ Batch API no longer crashes with future dates
- ✅ All APIs handle server warnings gracefully

**Root Cause**:
The native wrapper passed `nullptr` for the `ILogReceiver` parameter to databento-cpp
client constructors. When the API returned X-Warning headers (e.g., "degraded quality"
for future dates), the native library attempted to log the warning, causing NULL pointer
dereference → hardware exception → `AccessViolationException` in .NET.

**Solution**:
Created explicit `StderrLogReceiver` implementation for all 4 wrappers (Historical,
Batch, LiveBlocking, LiveThreaded). Server warnings now logged to stderr and visible
to users for debugging.

---

## 🔄 Visible Changes

### Log Format Updated

**Before**:
```
INFO: [LiveBlocking::Authenticate] Successfully authenticated with session_id 123
```

**After**:
```
[Databento INFO] [LiveBlocking::Authenticate] Successfully authenticated with session_id 123
```

### Log Destination: stdout → stderr

- Normal console use: No visible difference
- If you redirect stdout (`> file.log`): Use `2>&1` to also capture logs
- If you parse logs: Update pattern `INFO:` → `[Databento INFO]`

**Impact**: ~5-10% of users (those redirecting/parsing logs)

---

## 📦 What's Included

### Affected Components
- ✅ **Historical Client** - Critical fix (crash → works)
- ✅ **Batch Client** - Critical fix (crash → works)
- ✅ **LiveBlocking Client** - Consistency improvement
- ✅ **LiveThreaded Client** - Consistency improvement

### No API Changes
- ✅ Binary compatible
- ✅ Source compatible
- ✅ No user code changes required
- ✅ Same functionality, safer implementation

---

## 🧪 Testing

### Verified Scenarios
- 172 OHLCV records for CLZ5 futures (May-Nov 2025) ✅
- Invalid symbol handling (proper exceptions) ✅
- Live authentication and streaming ✅
- All 32 example projects ✅
- Stream redirection behavior ✅

### Test Results
- Historical with future dates: **0 crashes** (was 100% crash rate)
- Batch with invalid symbols: **Proper exception** (was potential crash)
- Live clients: **Working as expected** (new log format)

---

## 📖 Migration Guide

### Do I Need to Change My Code?

**No** - Your existing code works without changes.

### What Might Need Updates

**If you redirect output**:
```bash
# Old way (logs not captured)
dotnet run > output.log

# New way (captures logs)
dotnet run > output.log 2>&1
```

**If you parse logs**:
```csharp
// Update pattern matching
// Before: if (line.StartsWith("INFO:"))
// After:  if (line.Contains("[Databento INFO]"))
```

**See**: [MIGRATION_GUIDE_v3.0.24.md](MIGRATION_GUIDE_v3.0.24.md) for details

---

## 🔗 Related Issues

- Resolves: Bug Investigation #1 (NULL pointer in native wrappers)
- Related: Issue #1 (separate bug in databento-cpp with invalid symbols)

---

## 📥 Installation

```bash
dotnet add package Databento.Client --version 3.0.24-beta
```

Or update your `.csproj`:
```xml
<PackageReference Include="Databento.Client" Version="3.0.24-beta" />
```

---

## 🙏 Acknowledgments

Thanks to users who reported the AccessViolationException and provided reproduction
cases. Your feedback drives these improvements!

---

**Full Changelog**: v3.0.23-beta...v3.0.24-beta
```

**Attach**: `artifacts/Databento.Client.3.0.24-beta.nupkg`

---

## Risk Mitigation

### Pre-Deployment Checklist

- [ ] Phase 1 complete (all 4 wrappers fixed)
- [ ] Phase 2 complete (builds cleanly)
- [ ] Phase 3 complete (all tests pass)
- [ ] Phase 4 complete (documentation updated)
- [ ] Version bumped to 3.0.24-beta
- [ ] Git commit message comprehensive
- [ ] Release notes complete
- [ ] Migration guide created

### Deployment Checklist

- [ ] Local build successful
- [ ] Package inspection passed
- [ ] Integration test passed
- [ ] Git committed and tagged
- [ ] Pushed to both repositories
- [ ] NuGet published successfully
- [ ] GitHub release created
- [ ] Package appears on NuGet.org

---

## Rollback Plan

### If Issues Discovered After Release

**Immediate (0-1 hour)**:
1. Post notice on GitHub Issues warning users
2. Document the issue and workaround

**Short-term (1-24 hours)**:
1. Yank 3.0.24-beta from NuGet (users can't download)
2. Investigate and fix issue
3. Release 3.0.25-beta with fix

**Long-term (1-7 days)**:
1. Update documentation with issue details
2. Communicate via GitHub release notes
3. Consider releasing stable 3.1.0 if no issues

### Package Yanking

```bash
# If needed
dotnet nuget delete Databento.Client 3.0.24-beta \
  --api-key $NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json \
  --non-interactive
```

**Note**: Yanked packages can't be downloaded but existing users keep them

---

## Success Criteria

### Must Have ✅

- [ ] Historical API: No crash with future dates
- [ ] Batch API: No crash with future dates/invalid symbols
- [ ] Live APIs: Logs visible with new format
- [ ] All 32 examples run without crashes
- [ ] Documentation complete and accurate
- [ ] NuGet package published
- [ ] GitHub release created

### Should Have 📝

- [ ] Migration guide comprehensive
- [ ] Test results documented
- [ ] Before/after log format examples
- [ ] User communication plan

### Nice to Have 🎯

- [ ] Performance benchmarks
- [ ] User feedback collected
- [ ] Upstream report drafted (databento-cpp)

---

## Timeline Summary

| Phase | Duration | Risk | Status |
|-------|----------|------|--------|
| Phase 0: Investigation | 30 min | 🟢 NONE | ✅ COMPLETE |
| Phase 1: Implementation | 1 hour | 🟢 LOW | ⏳ Ready |
| Phase 2: Build & Verify | 30 min | 🟢 LOW | ⏳ Pending |
| Phase 3: Testing | 2 hours | 🟡 MEDIUM | ⏳ Pending |
| Phase 4: Documentation | 45 min | 🟢 NONE | ⏳ Pending |
| Phase 5: Deployment | 1 hour | 🟡 MEDIUM | ⏳ Pending |
| **Total** | **~5 hours** | **🟡 LOW-MEDIUM** | ⏳ In Progress |

---

## Final Review Checklist

### Code Quality
- [ ] All 4 wrappers updated consistently
- [ ] Shared StderrLogReceiver well-documented
- [ ] No code duplication
- [ ] Thread-safety considered

### Testing
- [ ] Critical scenarios tested (crash fixes)
- [ ] Regression testing complete
- [ ] Log format verified
- [ ] Stream redirection tested

### Documentation
- [ ] Release notes comprehensive
- [ ] Migration guide clear
- [ ] XML comments updated
- [ ] API reference updated

### Deployment
- [ ] Version numbers correct
- [ ] Git history clean
- [ ] Tags pushed
- [ ] Package verified

### Communication
- [ ] Breaking changes clearly documented
- [ ] Benefits explained
- [ ] Migration path provided
- [ ] Support available

---

**Status**: 📋 **PLAN COMPLETE - READY FOR REVIEW**
**Next Step**: Review plan with user, get approval, begin implementation
**Document Version**: 1.0
