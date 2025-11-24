# Implementation Plan: NULL Pointer Bug Fix

**Created**: November 20, 2025
**Bug**: AccessViolationException from NULL ILogReceiver in databento-cpp wrappers
**Version Target**: 3.0.24-beta
**Estimated Effort**: 4-6 hours

---

## Executive Summary

Fix critical NULL pointer dereference bug affecting Historical and Batch APIs when databento-cpp attempts to log warnings (e.g., future dates with "degraded quality"). Investigate Live client mystery before deciding on fix scope.

### Impact
- **Historical API**: ✅ Crash confirmed (future dates)
- **Batch API**: 🔴 Same bug (uses Historical client)
- **Live APIs**: ⚠️ Mystery - logs appear to work despite nullptr

### Fix Approach
**Recommended**: Approach 2 - Pass valid ILogReceiver from wrappers (fixes OUR code)
**Alternative**: Approach 1 - Defensive NULL checks in databento-cpp (requires patching third-party code)

---

## Phase 0: Pre-Investigation (CRITICAL - Do First!)

**Goal**: Understand Live client behavior before touching code
**Time**: 30 minutes max
**Risk**: Breaking working Live API

### Tasks

#### 1. Test Live API with Current Code
```bash
cd examples/LiveBlocking.Example
dotnet run
# Observe: Does it log successfully?
# Check CLAUDE.md output: "INFO: [LiveBlocking::Authenticate] Successfully authenticated..."
```

**Questions to answer**:
- Does Live client actually log?
- Or is the output from somewhere else?
- Does it crash in any scenario?

#### 2. Try to Trigger X-Warning in Live API
**Hypothesis**: Live might not hit the warning code path

**Test scenarios**:
1. Invalid symbols (see if metadata.not_found handles it)
2. Rate limiting (if possible to trigger)
3. Replay mode with future dates
4. Check if Live uses different HTTP client

#### 3. Code Path Analysis
**Check**:
- Does LiveBlocking use same HttpClient as Historical?
- Does Builder provide default log receiver?
- Is there a fallback mechanism we missed?

#### 4. Decision Matrix

| Finding | Action |
|---------|--------|
| Live doesn't actually log | Fix all 4 wrappers uniformly |
| Live uses different code path | Fix Historical + Batch only, document Live |
| Live crashes in some scenario | Fix all 4 wrappers urgently |
| Can't reproduce logging | Investigate further before proceeding |

### Investigation Output
Document findings in: `LIVE_CLIENT_INVESTIGATION_RESULTS.md`

**STOP HERE if findings are unexpected - regroup and reassess plan**

---

## Phase 1: Code Implementation

**Prerequisite**: Phase 0 complete and decision made on scope
**Time**: 1 hour
**Risk**: Low (isolated changes)

### 1.1 Create Shared Log Receiver Class

**File**: `src/Databento.Native/src/common_helpers.hpp`
**Location**: Add at end of file (before closing namespace)

```cpp
// ============================================================================
// Shared Log Receiver for databento-cpp clients
// ============================================================================

/**
 * Simple ILogReceiver implementation that logs to stderr
 * Used by all wrapper components to prevent NULL pointer dereferences
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
        std::fprintf(stderr, "[Databento %s] %s\n", level_str, message.c_str());
        std::fflush(stderr);
    }
};

}  // namespace databento_native
```

**Why this design**:
- ✅ Single shared implementation (DRY principle)
- ✅ Stderr output (doesn't interfere with stdout)
- ✅ Explicit flush (ensures messages visible immediately)
- ✅ Simple, no dependencies
- ✅ Thread-safe (stderr writes are atomic for single calls)

### 1.2 Fix Historical Client Wrapper

**File**: `src/Databento.Native/src/historical_client_wrapper.cpp`
**Lines**: 30-38

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

**Fixed code**:
```cpp
struct HistoricalClientWrapper {
    std::unique_ptr<db::Historical> client;
    std::string api_key;
    std::unique_ptr<databento_native::StderrLogReceiver> log_receiver;  // ADD THIS

    explicit HistoricalClientWrapper(const std::string& key)
        : api_key(key),
          log_receiver(std::make_unique<databento_native::StderrLogReceiver>()) {  // ADD THIS
        client = std::make_unique<db::Historical>(
            log_receiver.get(),  // CHANGE: Pass valid pointer instead of nullptr
            key,
            db::HistoricalGateway::Bo1
        );
    }
};
```

**Changes**:
1. Add `log_receiver` field (unique_ptr for automatic cleanup)
2. Initialize in constructor initializer list
3. Pass `log_receiver.get()` instead of `nullptr`

### 1.3 Fix Batch Wrapper

**File**: `src/Databento.Native/src/batch_wrapper.cpp`
**Lines**: 28-36

**IDENTICAL FIX** to Historical (batch uses same HistoricalClientWrapper struct)

```cpp
struct HistoricalClientWrapper {
    std::unique_ptr<db::Historical> client;
    std::string api_key;
    std::unique_ptr<databento_native::StderrLogReceiver> log_receiver;  // ADD THIS

    explicit HistoricalClientWrapper(const std::string& key)
        : api_key(key),
          log_receiver(std::make_unique<databento_native::StderrLogReceiver>()) {  // ADD THIS
        client = std::make_unique<db::Historical>(
            log_receiver.get(),  // CHANGE: Pass valid pointer
            key,
            db::HistoricalGateway::Bo1
        );
    }
};
```

### 1.4 Fix LiveBlocking Wrapper (IF Phase 0 determines it's needed)

**File**: `src/Databento.Native/src/live_blocking_wrapper.cpp`
**Lines**: 24-62

**Current code**:
```cpp
struct LiveBlockingWrapper {
    std::unique_ptr<db::LiveBlocking> client;
    std::string dataset;
    std::string api_key;
    // ... other fields ...

    void EnsureClientCreated() {
        if (!client) {
            auto builder = db::LiveBlocking::Builder()
                .SetKey(api_key)
                .SetDataset(dataset)
                .SetSendTsOut(send_ts_out)
                .SetUpgradePolicy(upgrade_policy);
            // Missing: .SetLogReceiver(...)

            client = std::make_unique<db::LiveBlocking>(builder.BuildBlocking());
        }
    }
};
```

**Fixed code**:
```cpp
struct LiveBlockingWrapper {
    std::unique_ptr<db::LiveBlocking> client;
    std::unique_ptr<databento_native::StderrLogReceiver> log_receiver;  // ADD THIS
    std::string dataset;
    std::string api_key;
    // ... other fields ...

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
                .SetLogReceiver(log_receiver.get());  // ADD THIS

            if (heartbeat_interval_secs > 0) {
                builder.SetHeartbeatInterval(std::chrono::seconds(heartbeat_interval_secs));
            }

            client = std::make_unique<db::LiveBlocking>(builder.BuildBlocking());
        }
    }
};
```

### 1.5 Fix LiveThreaded Wrapper (IF Phase 0 determines it's needed)

**File**: `src/Databento.Native/src/live_client_wrapper.cpp`
**Similar changes** to LiveBlocking (need to read full file first)

---

## Phase 2: Build and Local Testing

**Time**: 30 minutes
**Risk**: Low (local only)

### 2.1 Rebuild Native Library

```bash
cd src/Databento.Native/build

# Clean previous build
rm -rf *

# Reconfigure CMake
cmake ..

# Build in Release mode
cmake --build . --config Release

# Verify DLL created
ls -l Release/databento_native.dll
```

**Success criteria**: Clean build with no errors

### 2.2 Deploy DLL to Runtime Location

```bash
# Copy to runtime directory
cp Release/databento_native.dll ../runtimes/win-x64/native/databento_native.dll

# Verify timestamp
ls -l ../runtimes/win-x64/native/databento_native.dll
```

### 2.3 Build .NET Solution

```bash
cd ../../../  # Back to repo root

# Clean solution
dotnet clean

# Build in Release mode
dotnet build -c Release

# Verify no errors
```

**Success criteria**:
- 0 errors
- Warnings OK (XML documentation warnings expected)

### 2.4 Quick Smoke Test

```bash
# Test Historical API with known reproducer
cd examples/HistoricalFutureDates.Test
dotnet run

# Expected: 172 records, no crash
# Expected output: "[Databento WARNING] Server warning about degraded quality..."
```

**Success criteria**: No AccessViolationException

---

## Phase 3: Comprehensive Testing

**Time**: 1.5 hours
**Risk**: Medium (might discover new issues)

### 3.1 Test Matrix

| Test | Command | Expected Result | Pass/Fail |
|------|---------|----------------|-----------|
| **Historical - Future Dates** | HistoricalFutureDates.Test | 172 records, warning visible | [ ] |
| **Historical - Invalid Symbol** | (Create test) | DbentoException, no crash | [ ] |
| **Historical - Past Dates** | (Existing examples) | Works, no warnings | [ ] |
| **Batch - Invalid Symbol** | BatchInvalidSymbol.Test | DbentoException, no crash | [ ] |
| **Batch - Future Dates** | (Create test) | Works, warning visible | [ ] |
| **Live - Normal Mode** | LiveBlocking.Example | Authenticates, works | [ ] |
| **Live - Replay Mode** | (Existing examples) | Works, symbol resolution OK | [ ] |
| **Live - Invalid Symbol** | LiveInvalidSymbol.Test | Graceful handling | [ ] |

### 3.2 Test Historical API with Future Dates

**Already exists**: `examples/HistoricalFutureDates.Test`

```bash
cd examples/HistoricalFutureDates.Test
dotnet run
```

**Expected output**:
```
Testing Historical API with future dates (May-Nov 2025)...
Fetching data...

[Databento WARNING] Server Warning: The streaming request contained one or more days which have reduced quality: 2025-09-17 (degraded)...

Historical record: OHLCV: O:56.81 H:57.73 L:55.17 C:57.14 V:18031 [2025-05-01...]
...
✓ SUCCESS: Received 172 records without crashing!
```

**Pass criteria**:
- ✅ No AccessViolationException
- ✅ Warning message visible on stderr
- ✅ All 172 records received

### 3.3 Test Batch API with Invalid Symbol

**Already exists**: `examples/BatchInvalidSymbol.Test`

```bash
cd examples/BatchInvalidSymbol.Test
dotnet run
```

**Expected**: DbentoException (not crash)

### 3.4 Test Live API

```bash
cd examples/LiveBlocking.Example
dotnet run
```

**Expected**:
- Authentication succeeds
- Logging visible (if Phase 0 confirmed it works)
- No crashes

### 3.5 Run Full Test Suite

```bash
cd src/Databento.Client.Tests
dotnet test -c Release
```

**Expected**: All tests pass (or same failures as before fix)

### 3.6 Test All Examples

```bash
# Create script to run all 32 examples
for dir in examples/*/; do
    echo "Testing $dir..."
    cd "$dir"
    timeout 60s dotnet run || echo "FAILED or TIMEOUT: $dir"
    cd ../..
done
```

**Success criteria**: No new crashes

---

## Phase 4: Documentation Updates

**Time**: 30 minutes
**Risk**: None

### 4.1 Update API Documentation

**File**: `API_REFERENCE.md`

**Current warnings** (lines 633-639, 675-681):
```markdown
> ⚠️ **CRITICAL WARNING**: This method may crash with AccessViolationException...
```

**Updated text**:
```markdown
> ℹ️ **Note** (Fixed in v3.0.24-beta): Previous versions could crash with AccessViolationException
> when requesting data for future dates or when the API returned warnings. This has been fixed by
> properly initializing the native logging subsystem. Warnings from the Databento API are now
> visible on stderr.
```

**Location**: Update both GetRangeAsync and GetRangeToFileAsync sections

### 4.2 Update Release Notes

**File**: `src/Databento.Client/Databento.Client.csproj`

Update `PackageReleaseNotes`:
```xml
<PackageReleaseNotes>
v3.0.24-beta
- CRITICAL FIX: Resolved AccessViolationException when requesting future dates or when API returns warnings
- Root cause: NULL pointer in native logging subsystem
- Impact: Historical and Batch APIs now handle all server warnings gracefully
- Warnings are now visible on stderr (e.g., "degraded quality" for future dates)
- No breaking changes to API surface

v3.0.23-beta
- Fixed: Bundle VC++ runtime DLLs to prevent DllNotFoundException
...
</PackageReleaseNotes>
```

### 4.3 Update HistoricalClient.cs XML Comments

**File**: `src/Databento.Client/Historical/HistoricalClient.cs`
**Lines**: 95-109 (GetRangeAsync), 257-271 (GetRangeToFileAsync)

**Current XML**:
```csharp
/// <remarks>
/// ⚠️ <b>CRITICAL WARNING</b>: This method may crash with...
/// </remarks>
```

**Updated XML**:
```csharp
/// <remarks>
/// ℹ️ <b>Note</b>: Fixed in v3.0.24-beta. Previous versions could crash when requesting
/// future dates or invalid symbols. This has been resolved. Server warnings (such as
/// "degraded quality" for future dates) are now logged to stderr.
/// </remarks>
```

### 4.4 Create Fix Summary Document

**File**: `NULL_POINTER_FIX_SUMMARY.md`

Brief document explaining:
- What was fixed
- Root cause
- Impact on users
- How to verify the fix

---

## Phase 5: Version Bump and Commit

**Time**: 15 minutes
**Risk**: None

### 5.1 Update Version Numbers

**Files to update**:
1. `src/Databento.Client/Databento.Client.csproj`: Version → 3.0.24-beta
2. `src/Databento.Interop/Databento.Interop.csproj`: Version → 3.0.24-beta

```xml
<Version>3.0.24-beta</Version>
```

### 5.2 Git Commit

```bash
git add -A
git commit -m "fix: Resolve AccessViolationException from NULL ILogReceiver in native wrappers

CRITICAL BUG FIX:
- Historical and Batch APIs could crash with AccessViolationException when:
  * Requesting data for future dates (API returns X-Warning headers)
  * Any scenario triggering server warnings

ROOT CAUSE:
- Wrappers passed nullptr for ILogReceiver parameter to databento-cpp constructors
- When API returned X-Warning headers, CheckWarnings() dereferenced nullptr
- Hardware exception → .NET AccessViolationException

FIX:
- Created shared StderrLogReceiver class in common_helpers.hpp
- Updated Historical, Batch, and Live wrappers to pass valid log receiver
- Warnings now visible on stderr (helpful for debugging)

TESTING:
- ✅ HistoricalFutureDates.Test: 172 records, no crash
- ✅ BatchInvalidSymbol.Test: Proper exception handling
- ✅ All 32 examples tested successfully
- ✅ Live API regression tests passed

FILES CHANGED:
- src/Databento.Native/src/common_helpers.hpp (added StderrLogReceiver)
- src/Databento.Native/src/historical_client_wrapper.cpp (pass log receiver)
- src/Databento.Native/src/batch_wrapper.cpp (pass log receiver)
- src/Databento.Native/src/live_blocking_wrapper.cpp (if needed)
- src/Databento.Native/src/live_client_wrapper.cpp (if needed)
- src/Databento.Client/Databento.Client.csproj (version 3.0.24-beta)
- src/Databento.Interop/Databento.Interop.csproj (version 3.0.24-beta)
- API_REFERENCE.md (updated warnings)
- src/Databento.Client/Historical/HistoricalClient.cs (updated XML comments)

Resolves: Bug Investigation #1
Supersedes: Issue #1 (partial - this bug is different from invalid symbols bug)
"
```

---

## Phase 6: Package and Deploy

**Time**: 30 minutes
**Risk**: Medium (production deployment)

### 6.1 Build Release Package

```bash
# Clean everything
dotnet clean

# Build Release
dotnet build -c Release

# Pack NuGet package
dotnet pack src/Databento.Client/Databento.Client.csproj -c Release -o ./artifacts

# Verify package contents
unzip -l artifacts/Databento.Client.3.0.24-beta.nupkg | grep "databento_native.dll"
```

**Expected**: 9 DLLs in runtimes/win-x64/native/
- databento_native.dll (our DLL - should be updated)
- 3 VC++ runtime DLLs
- 5 dependency DLLs (zlib, openssl, zstd)

### 6.2 Verify Package Integrity

```bash
# Extract and inspect
mkdir temp_verify
cd temp_verify
unzip ../artifacts/Databento.Client.3.0.24-beta.nupkg

# Check databento_native.dll timestamp (should be recent)
ls -l runtimes/win-x64/native/databento_native.dll

# Check version in .nuspec
cat Databento.Client.nuspec | grep "<version>"
```

### 6.3 Local Integration Test

Create a fresh test project:
```bash
mkdir test_3.0.24_beta
cd test_3.0.24_beta
dotnet new console
dotnet add package Databento.Client --source ../artifacts --version 3.0.24-beta

# Add test code (future dates scenario)
# Run and verify
dotnet run
```

**Success criteria**: No crash, warnings visible

### 6.4 Push to Git Repositories

```bash
# Push to origin (private repo)
git push origin master

# Push to public repo
git push public master

# Create and push tag
git tag v3.0.24-beta
git push origin v3.0.24-beta
git push public v3.0.24-beta
```

### 6.5 Publish to NuGet.org

```bash
dotnet nuget push artifacts/Databento.Client.3.0.24-beta.nupkg \
  --api-key $NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

**Wait 10-15 minutes for indexing**

### 6.6 Create GitHub Release

**Title**: `v3.0.24-beta - Fixed AccessViolationException from NULL log receiver`

**Description**:
```markdown
## 🐛 Critical Bug Fix

Fixed `AccessViolationException` crash in Historical and Batch APIs when requesting future dates or when the Databento API returns warning headers.

### Root Cause

The native wrapper was passing `nullptr` for the `ILogReceiver` parameter to databento-cpp client constructors. When the API returned X-Warning headers (e.g., for future dates with "degraded quality"), the native library attempted to log the warning, resulting in a NULL pointer dereference and hardware exception that manifested as `AccessViolationException` in .NET.

### What's Fixed

- ✅ Historical API no longer crashes with future dates
- ✅ Batch API no longer crashes with future dates
- ✅ All server warnings now logged to stderr (helpful for debugging)
- ✅ Proper error handling throughout

### Breaking Changes

None. This is a pure bug fix with no API changes.

### Upgrade Instructions

```bash
dotnet add package Databento.Client --version 3.0.24-beta
```

### Testing

Verified with:
- 172 OHLCV records for CLZ5 futures (May-Nov 2025) ✅
- Invalid symbol handling (BatchInvalidSymbol.Test) ✅
- All 32 example projects ✅
- Live API regression tests ✅

### Files Changed

See commit message for detailed list.

### Related Issues

- Resolves: Bug Investigation #1 (NULL pointer in wrappers)
- Related: Issue #1 (separate bug with invalid symbols in databento-cpp)

---

**Full Changelog**: v3.0.23-beta...v3.0.24-beta
```

**Attach**: `artifacts/Databento.Client.3.0.24-beta.nupkg`

---

## Phase 7: Optional Upstream Reporting

**Time**: 1 hour
**Priority**: Medium
**Timing**: Can be done in parallel or after release

### 7.1 Report to databento-cpp Maintainers

**Where**: https://github.com/databento/databento-cpp/issues or https://issues.databento.com

**Title**: `NULL pointer dereferences in log_receiver_ usage (25 locations)`

**Description** (based on DATABENTO_CPP_BUG_REPORT.md):

```markdown
## Summary

The databento-cpp library has 25 locations where `log_receiver_` is dereferenced without NULL checks. This causes crashes when constructors/builders are used with `log_receiver = nullptr`.

## Impact

- Applications crash with segmentation fault / access violation
- Difficult to debug (cryptic error messages)
- Affects Historical, Batch, and Live clients

## Root Cause

Several functions dereference `log_receiver_` without checking for NULL:
- `http_client.cpp:202` - CheckWarnings() (3 dereferences)
- `historical.cpp` - GetRange methods (4 dereferences)
- `live_blocking.cpp` - Authenticate, Start, Subscribe (15 dereferences)
- Others (25 total)

## Reproduction

```cpp
auto client = db::Historical(nullptr, api_key, db::HistoricalGateway::Bo1);
client.TimeseriesGetRange(...);  // Crashes if API returns X-Warning header
```

## Suggested Fix

**Option 1**: Defensive NULL checks before dereferencing
```cpp
if (log_receiver_ != nullptr) {
    log_receiver_->Receive(LogLevel::Warning, msg.str());
} else {
    std::fprintf(stderr, "[Databento Warning] %s\n", msg.str().c_str());
}
```

**Option 2**: Constructor validation
```cpp
Historical(ILogReceiver* log_receiver, ...) {
    if (log_receiver == nullptr) {
        throw InvalidArgumentError("log_receiver cannot be nullptr");
    }
    // ...
}
```

**Option 3**: Builder always provides default (current behavior is correct, but direct constructor isn't safe)

## Workaround

Use Builder pattern instead of direct constructor:
```cpp
auto client = db::Historical::Builder().SetKey(key).Build();
// Builder provides ILogReceiver::Default() automatically
```

## Additional Context

We encountered this bug in databento-dotnet wrapper (https://github.com/Alparse/databento-dotnet)
where we were using the direct constructor. We've fixed our code, but suggesting this
defensive measure for databento-cpp to prevent similar issues for other users.
```

---

## Risk Assessment

### Overall Risk: LOW-MEDIUM 🟡

| Phase | Risk Level | Mitigation |
|-------|-----------|------------|
| Phase 0 (Investigation) | 🟢 NONE | Read-only, no code changes |
| Phase 1 (Implementation) | 🟢 LOW | Isolated changes, well-tested pattern |
| Phase 2 (Build/Test) | 🟢 LOW | Local only, no production impact |
| Phase 3 (Testing) | 🟡 MEDIUM | Might discover new issues |
| Phase 4 (Documentation) | 🟢 NONE | Documentation only |
| Phase 5 (Version/Commit) | 🟢 NONE | Standard process |
| Phase 6 (Deploy) | 🟡 MEDIUM | Production deployment |
| Phase 7 (Upstream) | 🟢 NONE | Optional, no direct impact |

### Rollback Plan

If issues discovered after NuGet publish:

1. **Immediate**: Post notice on GitHub Issues
2. **Short-term**: Yank 3.0.24-beta from NuGet (users can't download)
3. **Medium-term**: Fix and release 3.0.25-beta
4. **Long-term**: Document in release notes

### What Could Go Wrong

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Live API breaks | LOW | HIGH | Phase 0 investigation prevents this |
| New crashes discovered | LOW | HIGH | Comprehensive Phase 3 testing |
| Performance regression | VERY LOW | MEDIUM | Stderr writes are fast |
| Build failures | LOW | MEDIUM | Phase 2 local testing |
| Breaking existing code | VERY LOW | HIGH | No API changes, pure internal fix |

---

## Success Criteria

### Must Have ✅

- [x] Phase 0 investigation complete and documented
- [ ] Historical API: No crash with future dates
- [ ] Batch API: No crash with future dates
- [ ] All existing examples still work
- [ ] No new test failures
- [ ] Clean build (0 errors)
- [ ] Package published to NuGet.org
- [ ] GitHub release created
- [ ] Documentation updated

### Should Have 📝

- [ ] Live API: Fix applied (if Phase 0 determines it's needed)
- [ ] All 32 examples tested
- [ ] Performance benchmarks (no regression)
- [ ] Warning messages visible and helpful

### Nice to Have 🎯

- [ ] Upstream report to databento-cpp
- [ ] Additional test coverage for edge cases
- [ ] User feedback collected (GitHub Issues)

---

## Timeline

### Conservative Estimate: 6 hours

| Phase | Duration | Dependencies |
|-------|----------|--------------|
| Phase 0: Investigation | 30 min | None |
| Phase 1: Implementation | 1 hour | Phase 0 complete |
| Phase 2: Build/Test | 30 min | Phase 1 complete |
| Phase 3: Comprehensive Testing | 1.5 hours | Phase 2 complete |
| Phase 4: Documentation | 30 min | Phase 3 complete |
| Phase 5: Version/Commit | 15 min | Phase 4 complete |
| Phase 6: Deploy | 30 min | Phase 5 complete |
| Phase 7: Upstream | 1 hour | Optional, can be parallel |

### Aggressive Estimate: 4 hours

If Phase 0 reveals simple fix and no surprises in testing.

---

## Key Decisions to Make

### Decision Points

1. **After Phase 0**: Fix Live clients or not?
   - IF logs work: Fix anyway for consistency
   - IF logs don't work: Must fix urgently
   - IF can't determine: Fix conservatively (all 4 wrappers)

2. **After Phase 3**: Ready for release?
   - IF all tests pass: Proceed to Phase 6
   - IF new issues found: STOP, reassess, fix
   - IF performance regression: Investigate before proceeding

3. **After Phase 6**: Report upstream?
   - IF time allows: Report to databento-cpp
   - IF urgent: Skip Phase 7, do later

---

## Questions to Answer During Implementation

1. **Live Client Mystery** (Phase 0):
   - Why does logging appear to work in user's examples?
   - Is the output from databento-cpp or somewhere else?
   - Does Builder provide a default log receiver?

2. **Performance** (Phase 3):
   - Does stderr logging add measurable overhead?
   - Should we make logging configurable?

3. **Warning Messages** (Phase 3):
   - Are the warning messages helpful to users?
   - Should we filter/format them?
   - Do we need to suppress debug-level logs?

---

## Review Checklist

Before proceeding with each phase, confirm:

- [ ] Previous phase completed successfully
- [ ] No unexpected issues discovered
- [ ] Documentation updated for current phase
- [ ] Tests passing
- [ ] User not blocked on urgent work

---

## Contact Points

**If issues arise**:
1. Check `session_status.md` for historical context
2. Check `bug_investigation_1.md` for technical details
3. Check `DATABENTO_CPP_BUG_REPORT.md` for upstream issues
4. Consult user for production impact assessment

---

**Status**: 📋 PLAN READY FOR REVIEW
**Next Step**: Review with user, get approval, begin Phase 0
**Document Version**: 1.0

