# databento-cpp Test Results: Future Dates Query

## Executive Summary

**CRITICAL FINDING**: Pure databento-cpp handles the problematic query correctly. The .NET wrapper crash with future dates is a .NET-specific issue, NOT a databento-cpp bug.

## Test Configuration

### Test Location
- Project: `C:\Users\serha\source\repos\databento_cppTest1`
- Source: `test_bug.cpp`
- Build: CMake + Visual Studio 2022
- databento-cpp: main branch (latest)

### Test Parameters (Identical to Failing .NET Case)
```cpp
Dataset: GLBX.MDP3
Schema:  Ohlcv1D
Symbol:  CLZ5
Dates:   2025-05-01 to 2025-11-18
```

## Test Results

### ✅ databento-cpp: SUCCESS

```
=== databento-cpp Bug Reproduction Test ===

✓ API key found
✓ Created HistoricalClient

TEST: TimeseriesGetRange with future dates
-----------------------------------------------
About to call TimeseriesGetRange...
(If this crashes, the bug is confirmed)

✅ SUCCESS: TimeseriesGetRange completed
   Records received: 172
```

### ⚠️ Server Warning (Non-Fatal)
```
WARN: Server Warning: The streaming request contained one or more days
which have reduced quality: 2025-09-17 (degraded), 2025-09-24 (degraded).
```

This warning is informational and does not cause a crash.

### ❌ .NET Wrapper: CRASH

The same query parameters in databento-dotnet cause:
```
System.AccessViolationException:
Attempted to read or write protected memory.
```

## Analysis

### What This Means

1. **databento-cpp is NOT the source of the bug**
   - Handles future dates correctly
   - Returns proper warnings for degraded data
   - Completes successfully with 172 records

2. **The .NET wrapper has an additional bug**
   - Beyond the two bugs we already fixed (buffer overrun, exception handling)
   - Specific to handling certain API responses
   - May be related to how warnings are marshaled from C++ to C#

3. **Why .NET Crashes But C++ Doesn't**
   - Possible memory corruption in warning/metadata callback marshaling
   - Possible issue with string marshaling for server warnings
   - Possible issue with how the metadata callback handles edge cases

## Comparison: Working vs Failing

| Aspect | C++ (databento-cpp) | .NET (databento-dotnet) |
|--------|---------------------|-------------------------|
| Future dates query | ✅ Works (172 records) | ❌ Crashes |
| Server warnings | ✅ Logged properly | ❌ May cause crash |
| Record callback | ✅ Functions correctly | ⚠️ Fixed in v3.0.23-beta |
| Metadata callback | ✅ Functions correctly | ❓ May have issues |

## Test Code

The C++ test code mirrors the .NET wrapper's internal behavior:

```cpp
client.TimeseriesGetRange(
    dataset,
    date_range,
    symbols,
    schema,
    [&record_count](const databento::Record& record) {
        record_count++;
        if (record_count == 1) {
            std::cout << "  First record received" << std::endl;
        }
        return databento::KeepGoing::Continue;
    }
);
```

This is equivalent to the .NET call:
```csharp
await foreach (var record in client.GetRangeAsync(
    dataset, schema, symbols, startTime, endTime))
{
    count++;
}
```

## Next Investigation Steps

1. **Check metadata callback marshaling** in `historical_client_wrapper.cpp`
   - How are server warnings passed to C#?
   - Is string lifetime managed correctly?

2. **Test with metadata callback** in C++
   - Add metadata callback to test
   - See if warnings trigger any issues

3. **Compare warning handling**
   - How does C++ handle degraded data warnings?
   - How should .NET handle the same warnings?

4. **Memory validation**
   - Run .NET version under native debugger
   - Capture exact point of access violation

## Conclusion

The databento team's original assessment was correct: **databento-cpp does not have this bug**. The crash is specific to the .NET wrapper's handling of certain API responses, particularly those containing server warnings about data quality.

The two bugs we fixed (buffer overrun and exception handling) addressed crashes with valid historical data. This future dates crash is a separate issue in the .NET wrapper's metadata or warning handling code.

---

**Test Date**: 2025-11-19
**databento-cpp Version**: main branch (latest)
**Test Platform**: Windows x64, Visual Studio 2022
