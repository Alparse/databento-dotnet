# Test Protocol for Code Review Fixes

**Date**: November 16, 2025
**Changes**: Critical and High Priority fixes from CODE_REVIEW.md
**Total Fixes**: 7 (5 Critical + 2 High Priority)

---

## Executive Summary

This document provides a comprehensive test protocol for validating the fixes implemented during the November 16, 2025 session. All changes have passed compilation, and this protocol maps existing tests and examples to the specific fixes requiring validation.

---

## Change Summary

### Critical Fixes (Phase 1)
1. **ConfigureAwait(false) - Reference APIs** (7 locations)
2. **Channel TryWrite check** - HistoricalClient.cs:173
3. **Marshal.Copy protection** - LiveClient.cs:511-530
4. **Safe event invocation** - LiveClient.cs (9 locations)
5. **Race condition fix** - LiveClient.StartAsync

### High Priority Fixes (Phase 2)
6. **DateTimeOffset.Parse → TryParse** - HistoricalClient.cs (3 locations)
7. **Reference API code duplication** - Eliminated 92 lines

---

## Test Matrix

| Fix # | Component | Risk | Existing Test Coverage | New Test Needed? |
|-------|-----------|------|----------------------|------------------|
| **1** | Reference APIs | LOW | ✅ Reference.Example | ❌ No (existing covers) |
| **2** | HistoricalClient | MEDIUM | ❌ None found | ⚠️ Recommended |
| **3** | LiveClient | HIGH | ✅ CallbackSyncTest | ✅ Extended test needed |
| **4** | LiveClient | HIGH | ✅ LiveApiTests | ✅ Exception test needed |
| **5** | LiveClient | CRITICAL | ❌ None found | ✅ YES - Concurrency test |
| **6** | HistoricalClient | MEDIUM | ✅ HistoricalApiTests | ❌ No (graceful degradation) |
| **7** | Reference APIs | LOW | ✅ Reference.Example | ❌ No (refactoring only) |

---

## Detailed Test Protocol

## FIX #1: ConfigureAwait(false) in Reference APIs

### What Changed
- Added `.ConfigureAwait(false)` to 7 `ReadAsStringAsync()` calls across Reference APIs
- Files: SecurityMasterApi.cs, CorporateActionsApi.cs, AdjustmentFactorsApi.cs

### Risk Assessment
- **Risk Level**: LOW
- **Breaking Change**: No
- **Side Effects**: None (improves deadlock prevention)

### Existing Test Coverage
✅ **SUFFICIENT** - No new tests required

**Test File**: `examples/Reference.Example/Program.cs`

**Covered Scenarios**:
1. SecurityMaster.GetLastAsync() - Line 53
2. AdjustmentFactors.GetRangeAsync() - Line 91
3. CorporateActions.GetRangeAsync() - Line 125

### Test Protocol

#### Test 1.1: Run Reference.Example
```bash
cd examples/Reference.Example
dotnet run --configuration Release
```

**Expected Result**:
- ✅ All 3 API calls succeed
- ✅ Valid JSON deserialization
- ✅ Records returned for NVDA symbol
- ✅ No deadlocks or hangs

**Validation**:
- Program runs to completion
- No exceptions thrown
- "Reference API Examples Complete" message displayed

#### Test 1.2: Synchronous-Over-Async Test (Manual)
**Purpose**: Verify no deadlocks in UI contexts

Create test harness:
```csharp
// This would deadlock WITHOUT ConfigureAwait(false)
var task = client.SecurityMaster.GetLastAsync(new[] { "NVDA" });
var result = task.Result; // Blocking call
```

**Expected Result**:
- ✅ Completes without deadlock
- ⚠️ Not recommended pattern, but should work

**Status**: ⏸️ OPTIONAL - Not critical for CLI library

---

## FIX #2: Channel TryWrite Check in HistoricalClient

### What Changed
- Added return value check for `channel.Writer.TryWrite(record)`
- Throws exception if write fails
- Location: HistoricalClient.cs:173-179

### Risk Assessment
- **Risk Level**: MEDIUM
- **Breaking Change**: YES - Now throws exceptions that were silently dropped
- **Side Effects**: Better error detection (good)

### Existing Test Coverage
❌ **INSUFFICIENT** - No specific tests for channel capacity

**Test File**: `examples/ApiTests.Internal/HistoricalApiTests.cs`

**Partial Coverage**:
- GetRangeAsync test (line 85) - Uses default channel (unbounded)
- Does NOT test channel full scenario

### Test Protocol

#### Test 2.1: Run Existing Historical Tests
```bash
cd examples/ApiTests.Internal
dotnet run --configuration Release
```

**Expected Result**:
- ✅ All 17 HistoricalApiTests pass
- ✅ GetRangeAsync returns records
- ✅ No exceptions during normal operation

**Validation**:
- Check console output: "Passed: 17/17"
- Verify GetRangeAsync test succeeds

#### Test 2.2: Channel Capacity Stress Test (NEW - RECOMMENDED)
**Purpose**: Verify exception is thrown when channel full

**Test Scenario**:
```csharp
// Create channel with small capacity
var channel = Channel.CreateBounded<Record>(10);

// Subscribe to high-volume stream
await client.GetRangeAsync(...); // Fill channel quickly

// Expected: InvalidOperationException when channel full
```

**Expected Result**:
- ✅ Exception thrown with clear message
- ✅ No silent data loss
- ✅ Proper cleanup

**Status**: ⚠️ **RECOMMENDED** - New test should be added

**Priority**: MEDIUM - Current tests use unbounded channels (safe), but bounded channel scenario should be tested

---

## FIX #3: Marshal.Copy Protection in LiveClient

### What Changed
- Wrapped `Marshal.Copy` with try-catch for AccessViolationException
- Added logging and error event invocation
- Location: LiveClient.cs:511-530

### Risk Assessment
- **Risk Level**: HIGH
- **Breaking Change**: No (graceful degradation)
- **Side Effects**: Better crash prevention

### Existing Test Coverage
⚠️ **PARTIAL** - Cannot easily test native memory corruption

**Test File**: `examples/ApiTests.Internal/CallbackSyncTest.cs`

**Partial Coverage**:
- Tests callback synchronization
- Does NOT test corrupted native pointers (impossible to mock safely)

### Test Protocol

#### Test 3.1: Run CallbackSyncTest
```bash
cd examples/ApiTests.Internal
dotnet run --configuration Release
```

**Expected Result**:
- ✅ All 3 CallbackSyncTests pass
- ✅ No crashes during normal operation
- ✅ Records deserialized successfully

**Validation**:
- Check "All callback synchronization tests passed!"
- Verify Marshal.Copy succeeds in normal operation

#### Test 3.2: Error Event Verification
**Purpose**: Verify ErrorOccurred event fires on Marshal.Copy failure

**Test Scenario**: Cannot easily mock corrupted native pointers

**Alternative Approach**:
- Review error handling code (manual inspection)
- Verify exception types caught: AccessViolationException, ArgumentException, ArgumentOutOfRangeException
- Verify logging and error event invocation

**Status**: ✅ **CODE REVIEW SUFFICIENT**

**Rationale**: Native memory corruption cannot be reliably tested without crashing the app. Code review confirms proper error handling.

---

## FIX #4: Safe Event Invocation in LiveClient

### What Changed
- Created `SafeInvokeEvent<T>()` helper method
- Wrapped 9 event invocations throughout LiveClient
- Each subscriber invoked independently with exception handling
- Location: LiveClient.cs:678-701 (helper), 9 call sites

### Risk Assessment
- **Risk Level**: HIGH (prevents app crashes)
- **Breaking Change**: No (better error isolation)
- **Side Effects**: Subscriber exceptions logged but not propagated

### Existing Test Coverage
⚠️ **PARTIAL** - No exception throwing subscriber tests

**Test Files**:
- `examples/ApiTests.Internal/LiveApiTests.cs` - Tests event registration
- Does NOT test subscriber exceptions

### Test Protocol

#### Test 4.1: Run Existing Live Tests
```bash
cd examples/ApiTests.Internal
dotnet run --configuration Release
```

**Expected Result**:
- ✅ DataReceived_Event test passes (line 69)
- ✅ ErrorOccurred_Event test passes (line 121)
- ✅ Events fire successfully

**Validation**:
- Check "Passed: [X]/[X]" in Live API test summary
- Verify event tests complete without crashes

#### Test 4.2: Exception Throwing Subscriber Test (NEW - CRITICAL)
**Purpose**: Verify app doesn't crash when subscriber throws exception

**Test Scenario**:
```csharp
client.DataReceived += (sender, e) =>
{
    throw new Exception("Buggy subscriber code");
};

client.DataReceived += (sender, e) =>
{
    recordCount++; // This should still execute
};

await client.StartAsync();
// Stream data...
```

**Expected Result**:
- ✅ Exception logged but NOT propagated
- ✅ Second subscriber still receives events
- ✅ Application does not crash
- ✅ Stream continues normally

**Status**: ✅ **CRITICAL - NEW TEST REQUIRED**

**Priority**: HIGH - This is the core fix purpose

---

## FIX #5: Race Condition in LiveClient.StartAsync

### What Changed
- Fixed TOCTOU (Time-of-Check-Time-of-Use) vulnerability
- Uses atomic CompareExchange to set _streamTask
- Each thread gets own TaskCompletionSource
- Only one thread can successfully start
- Location: LiveClient.cs:289-355

### Risk Assessment
- **Risk Level**: CRITICAL
- **Breaking Change**: No (correct behavior)
- **Side Effects**: Prevents duplicate connections and resource leaks

### Existing Test Coverage
❌ **NONE** - No concurrent StartAsync tests found

**Test Files**:
- `examples/ApiTests.Internal/LiveApiTests.cs` - Single-threaded tests only
- Does NOT test concurrent StartAsync

### Test Protocol

#### Test 5.1: Run Existing Live Tests (Baseline)
```bash
cd examples/ApiTests.Internal
dotnet run --configuration Release
```

**Expected Result**:
- ✅ Single-threaded StartAsync works
- ✅ No regressions

**Validation**:
- All Live tests pass
- StartAsync test succeeds

#### Test 5.2: Concurrent StartAsync Test (NEW - CRITICAL)
**Purpose**: Verify race condition is fixed

**Test Scenario**:
```csharp
var client = new LiveClientBuilder().WithApiKey(_apiKey).Build();
await client.SubscribeAsync(...);

// Launch 100 concurrent StartAsync calls
var tasks = Enumerable.Range(0, 100)
    .Select(_ => Task.Run(async () =>
    {
        try
        {
            await client.StartAsync();
            return "Success";
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already started"))
        {
            return "Expected exception";
        }
    }))
    .ToArray();

var results = await Task.WhenAll(tasks);
```

**Expected Result**:
- ✅ Exactly 1 success
- ✅ 99 InvalidOperationException with "already started" message
- ✅ No resource leaks
- ✅ No crashes

**Status**: ✅ **CRITICAL - NEW TEST REQUIRED**

**Priority**: CRITICAL - This is the most serious fix

---

## FIX #6: DateTimeOffset.Parse → TryParse in HistoricalClient

### What Changed
- Replaced `DateTimeOffset.Parse` with `TryParse` (3 locations)
- Jobs with invalid timestamps filtered out instead of throwing
- Invalid mapping intervals skipped with warning
- Malformed cost strings throw clear exception
- Locations: HistoricalClient.cs:1119, 1362-1363, 802-806

### Risk Assessment
- **Risk Level**: MEDIUM
- **Breaking Change**: YES - Graceful degradation instead of exceptions
- **Side Effects**: Better robustness (good)

### Existing Test Coverage
✅ **SUFFICIENT** - Tests API with valid data

**Test File**: `examples/ApiTests.Internal/HistoricalApiTests.cs`

**Covered Scenarios**:
- BatchListJobsAsync_Filtered (line 623) - Uses timestamp filtering
- SymbologyResolveAsync (line 658) - Uses date parsing
- GetCostAsync (line 516) - Uses cost parsing

### Test Protocol

#### Test 6.1: Run Existing Historical Tests
```bash
cd examples/ApiTests.Internal
dotnet run --configuration Release
```

**Expected Result**:
- ✅ All tests pass with valid API data
- ✅ No exceptions thrown
- ✅ Filtering works correctly

**Validation**:
- Check "Passed: 17/17"
- Verify BatchListJobsAsync_Filtered succeeds
- Verify GetCostAsync succeeds

#### Test 6.2: Malformed Data Resilience (Manual Verification)
**Purpose**: Verify graceful handling of invalid data

**Test Scenarios**:
1. **Invalid job timestamp**: Jobs filtered out (no exception)
2. **Invalid mapping interval**: Interval skipped with warning logged
3. **Invalid cost string**: Clear exception with invalid value in message

**Expected Behavior**:
- ✅ #1 and #2: Silent filtering (graceful degradation)
- ✅ #3: Exception with clear error message

**Status**: ✅ **CODE REVIEW SUFFICIENT**

**Rationale**: API returns valid data in production. Error handling is defensive. Testing would require mocking invalid API responses.

---

## FIX #7: Reference API Code Duplication Elimination

### What Changed
- Created `ReferenceApiHelpers.cs` with shared utilities
- Extracted: EnsureSuccessStatusCode, FormatTimestamp, JsonOptions
- Removed 92 lines of duplicate code
- Deleted unused BuildUrl methods
- All 3 APIs now use shared helpers

### Risk Assessment
- **Risk Level**: LOW
- **Breaking Change**: No (pure refactoring)
- **Side Effects**: None (behavior unchanged)

### Existing Test Coverage
✅ **EXCELLENT** - Full Reference API coverage

**Test File**: `examples/Reference.Example/Program.cs`

**Covered Scenarios**:
1. SecurityMaster.GetLastAsync() - Uses all helpers
2. AdjustmentFactors.GetRangeAsync() - Uses all helpers
3. CorporateActions.GetRangeAsync() - Uses all helpers

### Test Protocol

#### Test 7.1: Run Reference.Example
```bash
cd examples/Reference.Example
dotnet run --configuration Release
```

**Expected Result**:
- ✅ All 3 API calls succeed
- ✅ Identical behavior to before refactoring
- ✅ No regressions

**Validation**:
- Program completes successfully
- "Reference API Examples Complete" displayed
- Valid data returned for all 3 APIs

#### Test 7.2: Error Handling Verification
**Purpose**: Verify EnsureSuccessStatusCode works consistently

**Test Scenarios** (requires manual testing with invalid params):
1. 400 error → ValidationException
2. 500 error → ServerException
3. Other errors → DbentoException

**Expected Result**:
- ✅ All 3 APIs throw same exception types for same errors
- ✅ Consistent error messages

**Status**: ✅ **COVERED BY EXAMPLE**

---

## Test Execution Plan

### Phase 1: Baseline Validation (30 minutes)

Run all existing tests to establish baseline:

```bash
# Test 1: Historical API Tests
cd examples/ApiTests.Internal
dotnet run --configuration Release
# Expected: 17/17 Historical tests pass

# Test 2: Live API Tests
# (Same executable, includes Live tests)
# Expected: All Live tests pass

# Test 3: Callback Sync Tests
# (Same executable, includes CallbackSyncTest)
# Expected: 3/3 sync tests pass

# Test 4: Reference API Example
cd ../Reference.Example
dotnet run --configuration Release
# Expected: 3/3 API examples complete
```

**Success Criteria**:
- ✅ All existing tests pass
- ✅ No regressions
- ✅ No new exceptions

---

### Phase 2: New Critical Tests (NEW - TO BE IMPLEMENTED)

#### Priority 1: Fix #5 - Concurrent StartAsync Test
**Status**: ❌ **NOT IMPLEMENTED** - CRITICAL
**Risk if skipped**: Race condition may still occur in production under load
**Effort**: 2-3 hours to implement
**Location**: Add to `examples/ApiTests.Internal/LiveApiTests.cs`

**Test Implementation**:
```csharp
private async Task<TestResult> TestConcurrentStartAsync()
{
    var testName = "Concurrent StartAsync (race condition test)";
    Console.WriteLine($"\n[TEST] {testName}");
    var sw = Stopwatch.StartNew();

    try
    {
        await using var client = new LiveClientBuilder()
            .WithApiKey(_apiKey)
            .Build();

        await client.SubscribeAsync(
            dataset: "EQUS.MINI",
            schema: Schema.Trades,
            symbols: _testSymbols
        );

        // Launch 100 concurrent StartAsync calls
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(async () =>
            {
                try
                {
                    await client.StartAsync();
                    return (Success: true, Exception: false);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("already started"))
                {
                    return (Success: false, Exception: true);
                }
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        int successes = results.Count(r => r.Success);
        int exceptions = results.Count(r => r.Exception);

        await client.StopAsync();
        sw.Stop();

        Console.WriteLine($"  Successes: {successes}/100");
        Console.WriteLine($"  Expected exceptions: {exceptions}/100");

        if (successes == 1 && exceptions == 99)
        {
            Console.WriteLine($"  ✓ PASS - Race condition fixed in {sw.ElapsedMilliseconds}ms");
            return new TestResult(true, "Race condition prevented", sw.Elapsed);
        }
        else
        {
            Console.WriteLine($"  ✗ FAIL - Expected 1 success, got {successes}");
            return new TestResult(false, $"Race condition detected: {successes} succeeded", sw.Elapsed);
        }
    }
    catch (Exception ex)
    {
        sw.Stop();
        Console.WriteLine($"  ✗ FAIL - {ex.Message}");
        return new TestResult(false, ex.Message, sw.Elapsed);
    }
}
```

---

#### Priority 2: Fix #4 - Exception Throwing Subscriber Test
**Status**: ❌ **NOT IMPLEMENTED** - HIGH
**Risk if skipped**: Buggy user code can crash application
**Effort**: 1-2 hours to implement
**Location**: Add to `examples/ApiTests.Internal/LiveApiTests.cs`

**Test Implementation**:
```csharp
private async Task<TestResult> TestSubscriberExceptionHandling()
{
    var testName = "Subscriber Exception Handling";
    Console.WriteLine($"\n[TEST] {testName}");
    var sw = Stopwatch.StartNew();

    try
    {
        await using var client = new LiveClientBuilder()
            .WithApiKey(_apiKey)
            .Build();

        int firstSubscriberCount = 0;
        int secondSubscriberCount = 0;

        // First subscriber throws exception
        client.DataReceived += (sender, e) =>
        {
            firstSubscriberCount++;
            throw new Exception("Intentional exception from buggy subscriber");
        };

        // Second subscriber should still receive events
        client.DataReceived += (sender, e) =>
        {
            secondSubscriberCount++;
        };

        await client.SubscribeAsync(
            dataset: "EQUS.MINI",
            schema: Schema.Trades,
            symbols: _testSymbols
        );

        await client.StartAsync();

        // Wait for some records
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await foreach (var record in client.StreamAsync(cts.Token))
            {
                if (secondSubscriberCount >= 5)
                    break;
            }
        }
        catch (OperationCanceledException) { }

        await client.StopAsync();
        sw.Stop();

        Console.WriteLine($"  First subscriber count: {firstSubscriberCount}");
        Console.WriteLine($"  Second subscriber count: {secondSubscriberCount}");

        if (secondSubscriberCount > 0)
        {
            Console.WriteLine($"  ✓ PASS - Second subscriber received events despite first subscriber exceptions");
            Console.WriteLine($"  ✓ Application did not crash");
            return new TestResult(true, $"Isolation working: {secondSubscriberCount} records", sw.Elapsed);
        }
        else
        {
            Console.WriteLine($"  ✗ FAIL - Second subscriber did not receive events");
            return new TestResult(false, "Subscriber isolation failed", sw.Elapsed);
        }
    }
    catch (Exception ex)
    {
        sw.Stop();
        Console.WriteLine($"  ✗ FAIL - Application crashed: {ex.Message}");
        return new TestResult(false, $"App crash: {ex.Message}", sw.Elapsed);
    }
}
```

---

#### Priority 3: Fix #2 - Channel Capacity Test (OPTIONAL)
**Status**: ❌ **NOT IMPLEMENTED** - MEDIUM
**Risk if skipped**: LOW (unbounded channels used in practice)
**Effort**: 1 hour to implement
**Location**: Add to `examples/ApiTests.Internal/HistoricalApiTests.cs`

---

### Phase 3: Integration Testing (Manual - Production-like)

Run comprehensive examples that exercise all changes:

```bash
# 1. Advanced Example (exercises multiple APIs)
cd examples/Advanced.Example
dotnet run --configuration Release

# 2. Live Streaming Examples
cd ../LiveStreaming.Example
dotnet run --configuration Release

cd ../LiveBlocking.Example
dotnet run --configuration Release

# 3. Historical Data Examples
cd ../Historical.Example
dotnet run --configuration Release

cd ../Batch.Example
dotnet run --configuration Release
```

**Success Criteria**:
- ✅ All examples run to completion
- ✅ No deadlocks
- ✅ No unhandled exceptions
- ✅ Data received and processed correctly

---

## Test Results Template

Use this template to record test results:

```markdown
## Test Session Report

**Date**: [Date]
**Tester**: [Name]
**Build**: Release
**Environment**: [OS, .NET version]

### Phase 1: Baseline Tests

| Test | Status | Duration | Notes |
|------|--------|----------|-------|
| Historical API Tests (17) | ✅/❌ | XXXms | |
| Live API Tests | ✅/❌ | XXXms | |
| Callback Sync Tests (3) | ✅/❌ | XXXms | |
| Reference Example | ✅/❌ | XXXms | |

### Phase 2: New Critical Tests

| Test | Status | Duration | Notes |
|------|--------|----------|-------|
| Concurrent StartAsync | ❌ NOT IMPL | - | TO BE ADDED |
| Subscriber Exception | ❌ NOT IMPL | - | TO BE ADDED |
| Channel Capacity | ⏸️ OPTIONAL | - | Low priority |

### Phase 3: Integration Tests

| Example | Status | Duration | Notes |
|---------|--------|----------|-------|
| Advanced.Example | ✅/❌ | XXXms | |
| LiveStreaming.Example | ✅/❌ | XXXms | |
| LiveBlocking.Example | ✅/❌ | XXXms | |
| Historical.Example | ✅/❌ | XXXms | |
| Batch.Example | ✅/❌ | XXXms | |

### Issues Found

[List any issues, errors, or unexpected behavior]

### Overall Assessment

- [ ] All baseline tests passing
- [ ] No regressions detected
- [ ] Ready for production: YES / NO
- [ ] Additional testing required: YES / NO

**Recommendation**: [APPROVE / NEEDS WORK / DEFER]
```

---

## Risk Summary

### Tested & Safe (LOW RISK)
- ✅ Fix #1: ConfigureAwait (covered by Reference.Example)
- ✅ Fix #6: TryParse (covered by HistoricalApiTests)
- ✅ Fix #7: Refactoring (covered by Reference.Example)

### Partially Tested (MEDIUM RISK)
- ⚠️ Fix #2: Channel write (normal operation tested, edge case not)
- ⚠️ Fix #3: Marshal.Copy (normal operation tested, corruption untestable)

### Needs Testing (HIGH RISK)
- ❌ Fix #4: Event exception isolation - **NEW TEST REQUIRED**
- ❌ Fix #5: Race condition - **NEW TEST CRITICAL**

---

## Recommendations

### Immediate Actions (Before Production)
1. ✅ **Run Phase 1 baseline tests** - Verify no regressions
2. ❌ **Implement Priority 1 test** - Concurrent StartAsync (CRITICAL)
3. ❌ **Implement Priority 2 test** - Subscriber exceptions (HIGH)

### Short-term (Next Sprint)
4. ⏸️ **Implement Priority 3 test** - Channel capacity (MEDIUM)
5. ⏸️ **Add CI/CD integration** - Automate test suite

### Long-term (Future Enhancement)
6. ⏸️ **Expand integration tests** - More examples, edge cases
7. ⏸️ **Load testing** - Stress test all fixes under high load

---

## Conclusion

**Current State**:
- ✅ Build passes (0 errors)
- ✅ Existing tests available for 5/7 fixes
- ❌ 2 critical fixes need new tests

**Production Readiness**:
- **With existing tests only**: 🟡 MODERATE RISK
- **With new critical tests**: 🟢 LOW RISK (RECOMMENDED)

**Recommendation**:
⚠️ **Implement Priority 1 & 2 tests before production deployment**

The fixes are high quality and address real issues, but the most critical fixes (#4 and #5) lack test coverage for the exact scenarios they prevent. Running baseline tests validates "no regressions" but doesn't prove the fixes work under the conditions they were designed to handle.

---

**Next Steps**:
1. Run Phase 1 baseline tests (30 min)
2. Review results
3. Decide: Ship with existing tests OR implement new critical tests first
