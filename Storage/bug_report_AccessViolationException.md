# Bug Report: AccessViolationException in databento-cpp TimeseriesGetRange with Invalid Symbols

## Summary

`databento-cpp` crashes with `AccessViolationException` (segmentation fault) when calling `TimeseriesGetRange()` with invalid symbol parameters. The crash occurs inside the native library before any error can be returned to the caller.

## Environment

- **databento-cpp version**: Latest (as of November 2025)
- **Platform**: Windows (x64)
- **Runtime**: .NET 8.0/9.0 via P/Invoke wrapper
- **Compiler**: MSVC (native DLL built with CMake)

## Steps to Reproduce

### Minimal C++ Example (Expected)
```cpp
#include <databento/historical.hpp>

int main() {
    auto client = databento::HistoricalBuilder()
        .SetKey(api_key)
        .Build();

    try {
        client->TimeseriesGetRange(
            "GLBX.MDP3",                           // dataset
            {"2025-11-01", "2025-11-02"},          // date range
            {"CL"},                                 // INVALID symbol (should be CLZ5)
            databento::Schema::Ohlcv1D,
            [](const databento::Record& record) {
                // Process record
                return databento::KeepGoing::Continue;
            }
        );
    } catch (const std::exception& e) {
        std::cerr << "Error: " << e.what() << std::endl;  // Should reach here
    }

    return 0;
}
```

### Actual Test Case (.NET P/Invoke)
```csharp
using Databento.Client.Builders;

var client = new HistoricalClientBuilder()
    .WithApiKey(Environment.GetEnvironmentVariable("DATABENTO_API_KEY"))
    .Build();

await foreach (var record in client.GetRangeAsync(
    dataset: "GLBX.MDP3",
    schema: Schema.Ohlcv1D,
    symbols: new[] { "CL" },  // INVALID - should be CLZ5
    startTime: new DateTimeOffset(DateTime.Parse("11/1/2025"), TimeSpan.Zero),
    endTime: new DateTimeOffset(DateTime.Parse("11/2/2025"), TimeSpan.Zero)))
{
    Console.WriteLine(record);
}
```

### Other Test Cases That Trigger the Bug
1. **Invalid symbol name**: `"CL"` instead of `"CLZ5"`
2. **Date range too large**: 6 months (5/1/2025 - 11/18/2025)
3. **Possibly other validation errors** returned by Databento API

## Expected Behavior

When the Databento Historical API returns an error response (HTTP 400/422) for invalid parameters:

**Option A**: databento-cpp should throw a C++ exception that can be caught:
```cpp
try {
    client->TimeseriesGetRange(...);
} catch (const databento::Exception& e) {
    // Caught: "Invalid symbol 'CL'"
}
```

**Option B**: databento-cpp should call the record callback with an `ErrorMessage` record (RType 0x15):
```cpp
[](const databento::Record& record) {
    if (record.RType() == 0x15) {  // ErrorMessage
        // Handle error
    }
    return databento::KeepGoing::Continue;
}
```

## Actual Behavior

Process crashes with `AccessViolationException` / `SIGSEGV`:

### Windows Stack Trace
```
Fatal error. System.AccessViolationException:
Attempted to read or write protected memory. This is often an indication that other memory is corrupt.

   at Databento.Interop.Native.NativeMethods.<dbento_historical_get_range>g____PInvoke|24_0(...)
   at Databento.Interop.Native.NativeMethods.dbento_historical_get_range(...)
   at Databento.Client.Historical.HistoricalClient+<>c__DisplayClass13_0.<GetRangeAsync>b__1()
```

### Linux Stack Trace
```
Segmentation fault (core dumped)
Exit code: 139 (SIGSEGV)
```

## Root Cause Analysis

The crash occurs at this location in the native wrapper:

**File**: `historical_client_wrapper.cpp`
**Function**: `dbento_historical_get_range`
**Line**: ~124 (inside `TimeseriesGetRange` call)

```cpp
DATABENTO_API int dbento_historical_get_range(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* schema,
    const char** symbols,
    size_t symbol_count,
    int64_t start_time_ns,
    int64_t end_time_ns,
    RecordCallback on_record,
    void* user_data,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        // ... validation code ...

        // CRASH OCCURS HERE:
        wrapper->client->TimeseriesGetRange(
            dataset,
            datetime_range,
            symbol_vec,
            schema_enum,
            [on_record, user_data](const db::Record& record) {
                // Callback code
                on_record(bytes, length, type, user_data);
                return db::KeepGoing::Continue;
            }
        );

        return 0;
    }
    catch (const std::exception& e) {
        // Exception handling - NEVER REACHED
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}
```

### Why Try/Catch Doesn't Help

The crash is an **AccessViolationException** (hardware-level memory fault), not a C++ exception. C++ `try/catch` cannot catch hardware exceptions like:
- Access violations (reading/writing invalid memory addresses)
- Segmentation faults (SIGSEGV on Linux)
- Null pointer dereferences
- Buffer overruns

This indicates a **memory safety bug** inside `databento-cpp` when processing error responses from the Databento API.

### Likely Cause

When the Databento API returns an HTTP error response (400/422) for invalid symbols:
1. databento-cpp receives the error response
2. Attempts to parse or process the error
3. Dereferences a null pointer, accesses freed memory, or causes buffer overrun
4. CPU raises hardware exception → process crashes

## Impact

### Severity: **HIGH**

This affects any application using databento-cpp that:
- Passes user input as symbols (potential for invalid symbols)
- Makes historical queries with invalid parameters
- Runs long-lived services (one bad request crashes entire process)

### Workarounds Attempted

1. ❌ **C++ try/catch**: Cannot catch hardware exceptions
2. ❌ **Pre-validation**: Cannot determine all invalid symbols without querying API
3. ❌ **Callback error handling**: Crash occurs before callback is invoked
4. ❌ **Error buffer checking**: `dbento_historical_get_range` never returns (crashes instead)

**No workaround exists** - the crash happens inside the native library.

## Additional Information

### Related API Behavior

The Databento Historical API correctly returns errors for invalid parameters:

**Example Error Response (HTTP 422)**:
```json
{
  "detail": {
    "case": "invalid_symbol",
    "message": "Symbol 'CL' is not valid. Did you mean 'CLZ5'?",
    "status_code": 422,
    "docs": "https://databento.com/docs/api-reference-historical/basics/symbology",
    "payload": {
      "dataset": "GLBX.MDP3",
      "invalid_symbols": ["CL"],
      "suggestions": ["CLZ5", "CLF5"]
    }
  }
}
```

The databento-cpp library should handle this response gracefully instead of crashing.

### Comparison with Other Databento Clients

The **Python client** handles invalid symbols correctly:
```python
try:
    client.timeseries.get_range(
        dataset="GLBX.MDP3",
        symbols=["CL"],  # Invalid
        schema="ohlcv-1d",
        start="2025-11-01",
        end="2025-11-02"
    )
except databento.BentoError as e:
    print(f"Error: {e}")  # Caught gracefully
```

The **C++ client** should provide similar error handling.

## Reproduction Rate

**100%** - Consistently crashes with invalid symbols or other API validation errors.

## Search for Existing Issues

Searched databento-cpp GitHub repository:
- **Open issues**: 0
- **Closed issues**: 4 (none related to crashes)
- **No reports** of AccessViolationException or segmentation faults
- **No reports** of issues with invalid symbol handling

This appears to be an **unreported bug**.

## Suggested Fix

1. **Add error response handling** in databento-cpp before accessing response data
2. **Add null pointer checks** when parsing API error responses
3. **Validate memory accesses** when processing error responses
4. **Add defensive checks** for all HTTP error status codes (4xx, 5xx)
5. **Return errors via exception** or ErrorMessage record instead of crashing

## Test Case for Verification

After fix is implemented, this test should pass:

```cpp
TEST(HistoricalClient, InvalidSymbolShouldNotCrash) {
    auto client = databento::HistoricalBuilder()
        .SetKey(api_key)
        .Build();

    bool error_caught = false;

    try {
        client->TimeseriesGetRange(
            "GLBX.MDP3",
            {"2025-11-01", "2025-11-02"},
            {"CL"},  // Invalid symbol
            databento::Schema::Ohlcv1D,
            [](const databento::Record& record) {
                return databento::KeepGoing::Continue;
            }
        );
    } catch (const databento::Exception& e) {
        error_caught = true;
        EXPECT_THAT(e.what(), HasSubstr("Invalid symbol"));
    }

    EXPECT_TRUE(error_caught) << "Should have thrown exception for invalid symbol";
}
```

## Contact Information

- **Reporter**: databento-dotnet maintainer
- **Issue Source**: GitHub issue #1 (https://github.com/Alparse/databento-dotnet/issues/1)
- **Date**: November 18, 2025

## Additional Resources

- User-reported issue: https://github.com/Alparse/databento-dotnet/issues/1
- Databento Historical API errors: https://databento.com/docs/api-reference-historical/basics/errors
- Stack trace and logs: Available upon request

---

**Priority**: High
**Category**: Memory Safety / Crash
**Component**: databento-cpp Historical API
**Affected Method**: `TimeseriesGetRange()`, `TimeseriesGetRangeToFile()`
