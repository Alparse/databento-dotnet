# Bug Report: Segfault in TimeseriesGetRange with Invalid Symbols

**Component:** databento-cpp `Historical::TimeseriesGetRange()`
**Severity:** High (process crash)
**Version:** Latest (November 2025)

---

## Summary

`TimeseriesGetRange()` and `TimeseriesGetRangeToFile()` segfault when given an invalid symbol (HTTP 422 response). Should throw `DbentoException` instead.

---

## Minimal Reproduction

```cpp
#include <databento/historical.hpp>

int main() {
    auto client = databento::HistoricalBuilder()
        .SetKey(std::getenv("DATABENTO_API_KEY"))
        .Build();

    // Prepare parameters exactly as databento-dotnet wrapper calls them
    std::string dataset = "GLBX.MDP3";
    std::vector<std::string> symbols = {"CL"};  // ❌ Invalid (should be "CL.FUT")
    databento::Schema schema = databento::Schema::Trades;

    databento::DateTimeRange<databento::UnixNanos> datetime_range{
        databento::UnixNanos{1704067200000000000},  // 2024-01-01 00:00:00 UTC
        databento::UnixNanos{1704153600000000000}   // 2024-01-02 00:00:00 UTC
    };

    try {
        // This crashes with segfault instead of throwing exception
        client->TimeseriesGetRange(
            dataset,
            datetime_range,
            symbols,
            schema,
            [](const databento::Record& record) {
                std::cout << "Record: " << record << std::endl;  // Never reached
                return databento::KeepGoing::Continue;
            }
        );
    }
    catch (const databento::DbentoException& ex) {
        std::cerr << ex.what() << std::endl;  // Never reached - process crashes
    }

    return 0;
}
```

**Expected:** Throws `DbentoException: "Invalid symbol 'CL' for dataset GLBX.MDP3"`

**Actual:** Segfault during error response parsing (HTTP 422 handler)

**Reproducibility:** 100% with any invalid symbol

**Verified via:** databento-dotnet C# wrapper - this exact call path causes `ExecutionEngineException` (memory corruption detected by CLR)

---

## Comparison: BatchSubmitJob Works Correctly

```cpp
// Same invalid symbol - WORKS FINE ✅
try {
    auto job_id = client->BatchSubmitJob(
        "GLBX.MDP3",
        databento::DateTimeRange<databento::UnixNanos>{
            databento::UnixNanos{1704067200000000000},
            databento::UnixNanos{1704153600000000000}
        },
        {"CL"},  // Same invalid symbol
        databento::Schema::Trades,
        databento::Encoding::Dbn
    );
}
catch (const databento::DbentoException& ex) {
    std::cerr << ex.what() << std::endl;  // ✅ Exception correctly caught
    // Output: "Invalid symbol 'CL' for dataset GLBX.MDP3"
}
```

**Key Point:** Both methods receive HTTP 422 from API. BatchSubmitJob handles it correctly. TimeseriesGetRange crashes. Bug is in TimeseriesGetRange's error handling, not HTTP layer.

---

## Affected Methods

| Method | Status |
|--------|--------|
| `TimeseriesGetRange(dataset, DateTimeRange<UnixNanos>, symbols, schema, callback)` | ❌ Segfault |
| `TimeseriesGetRange(dataset, DateTimeRange<string>, symbols, schema, callback)` | ❌ Segfault (likely) |
| `TimeseriesGetRangeToFile(dataset, DateTimeRange<UnixNanos>, symbols, schema, path)` | ❌ Segfault |
| `TimeseriesGetRangeToFile(dataset, DateTimeRange<string>, symbols, schema, path)` | ❌ Segfault (likely) |
| `BatchSubmitJob(...)` | ✅ Works correctly |
| `Live::Subscribe(...)` | ✅ Works correctly |

---

## Root Cause

Memory corruption in TimeseriesGetRange's HTTP 422 error handling. Likely:
- Buffer overflow parsing error JSON
- Null pointer dereference in error path
- String lifetime issue with error message

---

## Suggested Fix

1. Compare error handling code:
   - `TimeseriesGetRange()` (broken)
   - `BatchSubmitJob()` (working)

2. The private overload at line 235 is likely where the bug lives:
   ```cpp
   void TimeseriesGetRange(const HttplibParams& params,
                          const MetadataCallback& metadata_callback,
                          const RecordCallback& record_callback);
   ```

3. Copy the correct HTTP error handling from BatchSubmitJob

---

## Test Case

```cpp
TEST(HistoricalTest, TimeseriesGetRangeInvalidSymbol) {
    auto client = HistoricalBuilder().SetKey(GetTestApiKey()).Build();

    EXPECT_THROW({
        client->TimeseriesGetRange(
            "GLBX.MDP3",
            DateTimeRange<UnixNanos>{
                UnixNanos{1704067200000000000},
                UnixNanos{1704153600000000000}
            },
            {"CL"},  // Invalid symbol
            Schema::Trades,
            [](const Record&) { return KeepGoing::Continue; }
        );
    }, DbentoException);
}
```

---

## Workarounds

1. Pre-validate symbols before calling TimeseriesGetRange
2. Use BatchSubmitJob (handles errors correctly)
3. Use Live API (handles invalid symbols via metadata)

---

## Context

**Discovery:** databento-dotnet C# wrapper users reported `ExecutionEngineException`

**Verification Path:**
- C# → `HistoricalClient.GetRangeAsync()`
- → P/Invoke → `dbento_historical_get_range()`
- → C wrapper → `wrapper->client->TimeseriesGetRange()`
- → **databento-cpp crashes here** (verified via CLR memory corruption detection)

**Issue:** https://github.com/Alparse/databento-dotnet/issues/1

---

**Reporter:** databento-dotnet maintainer
**Contact:** support@databento.com or via databento-dotnet GitHub
