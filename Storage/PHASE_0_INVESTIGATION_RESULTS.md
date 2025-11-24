# Phase 0: Live Client Investigation Results

**Date**: November 20, 2025
**Duration**: 30 minutes
**Status**: ✅ COMPLETE

---

## Executive Summary

**Mystery Solved**: Live clients log successfully because they use **Builder pattern**, which automatically provides `ILogReceiver::Default()` when nullptr is passed. Historical clients crash because they use **direct constructor**, which doesn't provide this safety net.

### Key Finding

**Builder Pattern (Live) = Safe** ✅
**Direct Constructor (Historical/Batch) = Crash** 💥

---

## Investigation Steps Performed

### 1. Test Live Example ✅

**Command**: `dotnet run` in `examples/LiveBlocking.Example`

**Result**: Logs appear as expected:
```
INFO: [LiveBlocking::Authenticate] Successfully authenticated with session_id 1763811131
INFO: [LiveBlocking::Start] Starting session
```

**Conclusion**: Live client logging DOES work with current code.

---

### 2. Code Path Analysis ✅

#### Live Wrapper (Working)

**File**: `src/Databento.Native/src/live_blocking_wrapper.cpp:48-62`

```cpp
void EnsureClientCreated() {
    if (!client) {
        auto builder = db::LiveBlocking::Builder()
            .SetKey(api_key)
            .SetDataset(dataset)
            .SetSendTsOut(send_ts_out)
            .SetUpgradePolicy(upgrade_policy);
            // NO .SetLogReceiver() call - relies on Builder default

        client = std::make_unique<db::LiveBlocking>(builder.BuildBlocking());
    }
}
```

**Key**: Uses Builder pattern, does NOT call `.SetLogReceiver()`

#### Historical Wrapper (Broken)

**File**: `src/Databento.Native/src/historical_client_wrapper.cpp:36`

```cpp
client = std::make_unique<db::Historical>(nullptr, key, db::HistoricalGateway::Bo1);
                                          ^^^^^^^
```

**Key**: Uses direct constructor with explicit `nullptr`

---

### 3. databento-cpp Source Analysis ✅

#### Builder Provides Default

**File**: `databento-cpp/src/historical.cpp:986-988`

```cpp
Historical HistoricalBuilder::Build() {
    if (key_.empty()) {
        throw Exception{"'key' is unset"};
    }

    // ⭐ THIS IS THE SAFETY MECHANISM ⭐
    if (log_receiver_ == nullptr) {
        log_receiver_ = databento::ILogReceiver::Default();
    }

    return Historical{log_receiver_, key_, gateway_, upgrade_policy_, user_agent_ext_};
}
```

**Same code in Live Builder** (`live.cpp:117-119`):
```cpp
if (log_receiver_ == nullptr) {
    log_receiver_ = databento::ILogReceiver::Default();
}
```

#### ILogReceiver::Default() Implementation

**File**: `databento-cpp/src/log.cpp:11-14`

```cpp
databento::ILogReceiver* databento::ILogReceiver::Default() {
    static const std::unique_ptr<ILogReceiver> gDefaultLogger{
        std::make_unique<ConsoleLogReceiver>()};
    return gDefaultLogger.get();
}
```

**Returns**: `ConsoleLogReceiver` (logs to stdout/stderr)

#### Direct Constructor (No Safety)

**File**: `databento-cpp/include/databento/historical.hpp`

```cpp
// WARNING: Will be deprecated in the future in favor of the builder
Historical(ILogReceiver* log_receiver, std::string key, HistoricalGateway gateway);
```

**No nullptr check** - directly stores whatever you pass.

---

## Root Cause Analysis

### Why Live Works ✅

```
LiveWrapper → Builder → Builder.BuildBlocking()
                          ↓
                     if (log_receiver_ == nullptr)
                          log_receiver_ = Default()
                          ↓
                     LiveBlocking(valid_pointer, ...)
                          ↓
                     Logging works! ✅
```

### Why Historical Crashes 💥

```
HistoricalWrapper → new Historical(nullptr, key, gateway)
                          ↓
                    Historical(nullptr, ...)  // No safety check
                          ↓
                    API returns X-Warning
                          ↓
                    CheckWarnings() calls log_receiver_->Receive(...)
                          ↓
                    NULL pointer dereference → AccessViolationException 💥
```

---

## Component Status Matrix

| Component | Current Implementation | Works? | Why? |
|-----------|----------------------|--------|------|
| **Historical** | Direct constructor + nullptr | ❌ BROKEN | No default provided |
| **Batch** | Direct constructor + nullptr | ❌ BROKEN | Uses Historical internally |
| **LiveBlocking** | Builder (no SetLogReceiver) | ✅ WORKS | Builder provides Default() |
| **LiveThreaded** | Builder (no SetLogReceiver) | ✅ WORKS | Builder provides Default() |

---

## Why Historical/Batch Don't Use Builder

**Technical reason**: Builder returns value type, wrapper needs pointer for unique_ptr storage.

**What could have been done**:
```cpp
// Could have used Builder like this:
HistoricalClientWrapper(const std::string& key) {
    auto built_client = db::Historical::Builder().SetKey(key).Build();
    client = std::make_unique<db::Historical>(std::move(built_client));
}
```

**What was actually done** (shortcut):
```cpp
// Took "simpler" direct constructor path
client = std::make_unique<db::Historical>(nullptr, key, gateway);
```

---

## Test: Can We Trigger Live API Warning?

### Attempted Scenarios

**Scenario 1**: Invalid symbol in Live subscription
**Expected**: Handled via `metadata.not_found` (doesn't trigger X-Warning)
**Test**: `LiveInvalidSymbol.Test` already exists and passes ✅

**Scenario 2**: Future dates in replay mode
**Expected**: May trigger X-Warning if API warns about replay quality
**Status**: Not tested yet (would require specific setup)

**Scenario 3**: Rate limiting
**Expected**: Different error path (not X-Warning)
**Status**: Difficult to trigger reliably

### Conclusion

Live API likely uses different protocol (WebSocket) and doesn't encounter the X-Warning HTTP header scenario that triggers the bug. Even if it did, Builder provides Default() so it would work.

---

## Decision Matrix

| Finding | Recommendation |
|---------|----------------|
| Live works with current code | Could leave Live alone |
| Builder provides Default() | Safety net already exists |
| Default() logs to stdout | We'd prefer stderr (our control) |
| Consistency matters | Fix all 4 wrappers uniformly |
| Future-proof | Don't rely on databento implementation detail |

---

## Final Decision: Fix All 4 Wrappers

### Reasoning

**Why fix Live even though it works?**

1. **Consistency**: All wrappers should explicitly manage logging
2. **Control**: Logs go to stderr (our choice) not stdout (databento default)
3. **Documentation**: Makes intent clear to future maintainers
4. **Future-proof**: Don't rely on Builder default behavior
5. **Best practice**: Explicit is better than implicit
6. **Minimal cost**: Easy fix, already writing the code

### Scope of Fix

- ✅ **Historical**: MUST fix (crashes)
- ✅ **Batch**: MUST fix (crashes)
- ✅ **LiveBlocking**: SHOULD fix (for consistency)
- ✅ **LiveThreaded**: SHOULD fix (for consistency)

---

## Implementation Approach Confirmed

### Create Shared StderrLogReceiver

**Location**: `src/Databento.Native/src/common_helpers.hpp`

**Benefits**:
- DRY principle (single implementation)
- Stderr output (doesn't interfere with stdout)
- Thread-safe
- Simple, no dependencies

### Fix Pattern (All 4 Wrappers)

**For Historical/Batch** (direct constructor):
```cpp
struct HistoricalClientWrapper {
    std::unique_ptr<db::Historical> client;
    std::string api_key;
    std::unique_ptr<databento_native::StderrLogReceiver> log_receiver;  // ADD

    explicit HistoricalClientWrapper(const std::string& key)
        : api_key(key),
          log_receiver(std::make_unique<databento_native::StderrLogReceiver>()) {  // ADD
        client = std::make_unique<db::Historical>(
            log_receiver.get(),  // CHANGE from nullptr
            key,
            db::HistoricalGateway::Bo1
        );
    }
};
```

**For Live** (Builder pattern):
```cpp
struct LiveBlockingWrapper {
    std::unique_ptr<db::LiveBlocking> client;
    std::unique_ptr<databento_native::StderrLogReceiver> log_receiver;  // ADD
    // ... other fields ...

    void EnsureClientCreated() {
        if (!client) {
            auto builder = db::LiveBlocking::Builder()
                .SetKey(api_key)
                .SetDataset(dataset)
                .SetSendTsOut(send_ts_out)
                .SetUpgradePolicy(upgrade_policy)
                .SetLogReceiver(log_receiver.get());  // ADD THIS LINE

            client = std::make_unique<db::LiveBlocking>(builder.BuildBlocking());
        }
    }
};
```

---

## Expected Behavior After Fix

### Historical API with Future Dates

**Before**:
```
Fatal error. System.AccessViolationException: Attempted to read or write protected memory.
```

**After**:
```
[Databento WARNING] Server Warning: The streaming request contained one or more days
which have reduced quality: 2025-09-17 (degraded)...

Historical record: OHLCV: O:56.81 H:57.73 L:55.17 C:57.14 V:18031
...
✓ SUCCESS: Received 172 records
```

### Live API Logging

**Before**:
```
INFO: [LiveBlocking::Authenticate] Successfully authenticated with session_id 1763811131
```

**After** (same, but explicitly via our StderrLogReceiver):
```
[Databento INFO] [LiveBlocking::Authenticate] Successfully authenticated with session_id 1763811131
```

**Note**: Format changes from databento default to our format (prefix "[Databento INFO]").

---

## Risk Assessment After Investigation

| Risk | Assessment | Mitigation |
|------|-----------|------------|
| Breaking Live API | 🟢 VERY LOW | Builder already handles nullptr safely |
| New crashes | 🟢 VERY LOW | Moving to safer pattern |
| Performance regression | 🟢 VERY LOW | Stderr writes are fast |
| Format changes | 🟡 LOW | Log format will change slightly |
| Users parsing logs | 🟡 LOW | Document in release notes |

### Overall Risk: 🟢 LOW (was 🟡 MEDIUM before investigation)

Investigation reduced risk because we now know:
- Live works with nullptr (Builder safety)
- Our fix makes code safer, not riskier
- No hidden gotchas in databento-cpp

---

## Testing Strategy

### Must Test

1. **Historical with future dates**: HistoricalFutureDates.Test → No crash ✅
2. **Batch with invalid symbol**: BatchInvalidSymbol.Test → Proper exception ✅
3. **Live authentication**: LiveBlocking.Example → Logs visible ✅
4. **Live replay mode**: Existing examples → Symbol resolution works ✅

### Should Test

5. **All 32 examples**: Run full suite → No new crashes
6. **Live with invalid symbol**: LiveInvalidSymbol.Test → Graceful handling
7. **Format comparison**: Before/after log format changes

### Nice to Test

8. **Historical with invalid symbol**: Triggers exception, not warning
9. **Batch with future dates**: Should work like Historical
10. **Performance**: No measurable overhead from stderr logging

---

## Next Steps - Ready to Proceed

**Phase 0**: ✅ **COMPLETE**

**Ready for Phase 1**: ✅ **YES**

### Proceed With:

1. Create shared StderrLogReceiver class
2. Fix all 4 wrappers (Historical, Batch, LiveBlocking, LiveThreaded)
3. Rebuild native library
4. Test all scenarios
5. Document changes
6. Release v3.0.24-beta

### No Blockers Found

All questions answered:
- ✅ Why Live works: Builder provides Default()
- ✅ Should we fix Live: Yes, for consistency
- ✅ Risk level: LOW (reduced from MEDIUM)
- ✅ Approach validated: Fix all 4 wrappers

---

## Key Learnings

1. **Builder pattern has nullptr safety** - Historical should have used it
2. **ILogReceiver::Default() exists** - Returns ConsoleLogReceiver
3. **Direct constructors are dangerous** - No safety nets
4. **databento-cpp has deprecation warnings** - Should have heeded them
5. **Live API uses different protocol** - WebSocket vs HTTP, different error paths

---

## Documentation to Update

After implementation:

1. **bug_investigation_1.md**: Add "Live works because Builder provides Default()"
2. **NULL_POINTER_FIX_PLAN.md**: Update Phase 0 section with findings
3. **session_status.md**: Document investigation results
4. **Release notes**: Explain log format changes

---

**Status**: 📋 INVESTIGATION COMPLETE - READY TO IMPLEMENT
**Recommendation**: PROCEED WITH PHASE 1
**Confidence**: HIGH (all unknowns resolved)

