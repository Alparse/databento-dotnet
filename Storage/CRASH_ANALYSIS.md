# AccessViolationException Crash Analysis

## Current Status

The crash is STILL HAPPENING even with our fixes. This indicates the problem is DEEPER than the buffer overrun and exception handling bugs we fixed.

## What We Know

### ✅ Our Fixes ARE Being Used
- Debug output shows: `[C++ DEBUG v3.0.24] ===== FIXED VERSION =====`
- Package version 3.0.26-beta is installed
- Native DLL timestamp matches rebuild

### ❌ Crash Still Occurs
```
[C++ DEBUG v3.0.24] ===== FIXED VERSION ===== About to call TimeseriesGetRange
Fatal error. System.AccessViolationException
```

### ✅ Pure C++ Works Fine
- Same query parameters in pure databento-cpp: **172 records received, NO CRASH**
- This proves databento-cpp is NOT the problem

## The Mystery

### What's Different?
1. **Pure C++**: Uses simple C++ lambda callback → WORKS
2. **.NET Wrapper**: Uses C# delegate through P/Invoke → CRASHES

### Where It Crashes
The crash happens:
- AFTER: Debug print "About to call TimeseriesGetRange"
- BEFORE: Any callback is invoked (we never see callback debug output)
- DURING: The call to `client->TimeseriesGetRange()` inside databento-cpp

## Hypothesis

The crash is likely happening in ONE of these scenarios:

### Theory #1: databento-cpp HTTP Call
- databento-cpp makes HTTP request to API
- API returns data or error response
- databento-cpp tries to parse response
- **Crash occurs during parsing or callback preparation**

### Theory #2: Callback Function Pointer Issue
- C# delegate is passed through P/Invoke
- databento-cpp receives function pointer
- When databento-cpp tries to INVOKE the callback
- **Function pointer is corrupt or calling convention mismatch**

### Theory #3: String Parameter Corruption
- Dataset, schema, or symbol strings passed from C#
- Strings get corrupted or freed prematurely
- databento-cpp tries to use them during HTTP request
- **Access violation when accessing freed/corrupt string memory**

## What We've Ruled Out

✅ **NOT buffer overrun** - We fixed that, pure C++ works
✅ **NOT exception re-throw** - We fixed that
✅ **NOT databento-cpp bug** - Pure C++ works perfectly
✅ **NOT calling convention** - Delegate uses correct `CallingConvention.Cdecl`

## Evidence

### Stack Trace
```
at Databento.Interop.Native.NativeMethods.<dbento_historical_get_range>g____PInvoke|24_0(...)
at Databento.Interop.Native.NativeMethods.dbento_historical_get_range(...)
```

This shows the crash happens DURING the P/Invoke call, not after it returns.

### C++ Test Success
```cpp
client.TimeseriesGetRange(
    "GLBX.MDP3",
    {UnixNanos("2025-05-01"), UnixNanos("2025-11-18")},
    {"CLZ5"},
    Schema::Ohlcv1D,
    [](const Record& record) { ... }
);
// Result: ✅ 172 records, NO CRASH
```

## Next Steps

### Critical Investigation Needed

1. **Test with VALID historical data** (not future dates)
   - If it works: Problem is specific to this API response
   - If it crashes: Problem is in P/Invoke layer itself

2. **Test with different callback**
   - Create minimal C# callback that does NOTHING
   - If it works: Problem is in our callback code
   - If it crashes: Problem is in callback invocation mechanism

3. **Add try-catch in C++ wrapper**
   - Wrap `TimeseriesGetRange` call in try-catch
   - Log any C++ exceptions before they reach .NET
   - See if databento-cpp is throwing

4. **Use different query parameters**
   - Try different dataset (XNAS.ITCH instead of GLBX.MDP3)
   - Try different schema (Trades instead of Ohlcv1D)
   - Try valid dates (November 2024 instead of future)

## Comparison: What DOES Work

| Test Case | Result | Records |
|-----------|--------|---------|
| C++ - Future dates, OHLCV1D | ✅ Works | 172 |
| .NET - Valid dates, OHLCV1D | ❓ Unknown | ? |
| .NET - Valid dates, Trades | ✅ Worked in earlier tests | 114K |
| .NET - Future dates, OHLCV1D | ❌ CRASHES | 0 |

## The Smoking Gun?

The fact that:
- Pure C++ with EXACT same parameters works
- .NET wrapper with EXACT same parameters crashes
- Crash happens DURING databento-cpp's TimeseriesGetRange call

Suggests the problem is in HOW the callback is being invoked from C++ back to .NET, OR in how the C++ library is trying to use parameters/strings that were passed from .NET.

## Recommendation

**We need to test with VALID historical data** to see if the crash is:
1. Specific to this particular API response (future dates), OR
2. A general problem with the P/Invoke callback mechanism

If valid historical data works, then the issue is specific to how databento-cpp or the .NET wrapper handles certain API responses (errors, warnings, or specific data patterns).

---

**Status**: Investigation ongoing
**Priority**: CRITICAL - User cannot use Historical API with certain queries
**Workaround**: Use Batch API which doesn't crash
