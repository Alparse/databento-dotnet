# Databento.NET Example Test Report

**Test Date**: 2025-11-22 16:30:59
**Configuration**: Release
**DLL Version**: Clean Production (789K, no debug messages)

## Test Summary
**Total Examples**: 33

## Test Results

| # | Example | Status | Duration | Notes |
|---|---------|--------|----------|-------|
| 1 | Advanced.Example | âœ… PASSED | 2.86s | Build OK, DLL clean |
| 2 | ApiTests.Internal | âœ… PASSED | 2.05s | Build OK, DLL clean |
| 3 | Authentication.Example | âœ… PASSED | 1.94s | Build OK, DLL clean |
| 4 | Batch.Example | â­ï¸  SKIPPED | 1.88s | Requires live API / long-running |
| 5 | BatchInvalidSymbol.Test | â­ï¸  SKIPPED | 1.8s | Requires live API / long-running |
| 6 | DbnFileReader.Example | âœ… PASSED | 1.88s | Build OK, DLL clean |
| 7 | DiagnosticTest | â­ï¸  SKIPPED | 1.88s | Diagnostic test |
| 8 | DiagnosticTest2 | â­ï¸  SKIPPED | 1.92s | Diagnostic test |
| 9 | Errors.Example | âœ… PASSED | 1.74s | Build OK, DLL clean |
| 10 | Historical.Example | âœ… PASSED | 2.06s | Build OK, DLL clean |
| 11 | Historical.Readme.Example | âœ… PASSED | 2.11s | Build OK, DLL clean |
| 12 | HistoricalData.Example | âœ… PASSED | 2.03s | Build OK, DLL clean |
| 13 | HistoricalFutureDates.Test | âœ… PASSED | 2.23s | Build OK, DLL clean |
| 14 | IntradayReplay.Example | âœ… PASSED | 1.75s | Build OK, DLL clean |
| 15 | LiveAuthentication.Example | â­ï¸  SKIPPED | 1.69s | Requires live API / long-running |
| 16 | LiveBlocking.Comprehensive.Example | â­ï¸  SKIPPED | 1.72s | Requires live API / long-running |
| 17 | LiveBlocking.Example | â­ï¸  SKIPPED | 1.7s | Requires live API / long-running |
| 18 | LiveInvalidSymbol.Test | â­ï¸  SKIPPED | 1.75s | Requires live API / long-running |
| 19 | LiveStreaming.Example | â­ï¸  SKIPPED | 1.88s | Requires live API / long-running |
| 20 | LiveStreaming.Readme.Example | â­ï¸  SKIPPED | 2s | Requires live API / long-running |
| 21 | LiveSymbolResolution.Example | â­ï¸  SKIPPED | 2.3s | Requires live API / long-running |
| 22 | LiveThreaded.Comprehensive.Example | â­ï¸  SKIPPED | 1.71s | Requires live API / long-running |
| 23 | LiveThreaded.ExceptionCallback.Example | â­ï¸  SKIPPED | 1.86s | Requires live API / long-running |
| 24 | Metadata.Example | âœ… PASSED | 1.88s | Build OK, DLL clean |
| 25 | MultipleSubscriptions.Example | âœ… PASSED | 1.71s | Build OK, DLL clean |
| 26 | Reference.Example | âœ… PASSED | 1.77s | Build OK, DLL clean |
| 27 | SizeLimits.Example | âœ… PASSED | 1.93s | Build OK, DLL clean |
| 28 | Snapshot.Example | âœ… PASSED | 1.72s | Build OK, DLL clean |
| 29 | SnapshotSubscription.Example | âœ… PASSED | 1.69s | Build OK, DLL clean |
| 30 | StartWithMetadata.Example | âœ… PASSED | 1.65s | Build OK, DLL clean |
| 31 | SymbolMap.Example | âœ… PASSED | 2s | Build OK, DLL clean |
| 32 | Symbology.Example | âœ… PASSED | 2.14s | Build OK, DLL clean |
| 33 | TimestampValidationTest | âœ… PASSED | 1.94s | Build OK, DLL clean |

## Summary Statistics

- **Passed**: 20 / 33
- **Failed**: 0 / 33
- **Skipped**: 13 / 33

## DLL Verification

All examples built with clean production DLL:
- **Expected Size**: 789KB
- **Source**: `src/Databento.Interop/runtimes/win-x64/native/databento_native.dll`
- **No Debug Messages**: âœ… Verified

## Notes

- Live streaming examples skipped (require market hours)
- Batch examples skipped (long-running operations)
- All examples built successfully in Release configuration
- All DLLs verified to be clean production version (789KB)
- No `[C++ DEBUG]` messages present in any build

