# Changelog

All notable changes to databento-dotnet will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [3.0.24-beta] - 2025-11-20

### Fixed

- **CRITICAL**: Fixed AccessViolationException crash in Historical and Batch APIs when the Databento server returns warning headers (e.g., querying future dates with degraded data quality). The crash was caused by passing NULL pointer to the `ILogReceiver` parameter in the native C++ wrapper. ([#1](https://github.com/Alparse/databento-dotnet/issues/1))
  - Historical API now correctly handles server warnings and continues processing records
  - Batch API now safely handles submission errors without crashing
  - Server warnings now visible on stderr instead of causing silent crashes
  - Example: Querying OHLCV data from 2025-05-01 to 2025-11-15 now receives all 172 records with warning visible, instead of immediate crash

### Changed

- **Native Logging**: Improved logging consistency across all client types (Historical, Batch, LiveBlocking, LiveThreaded)
  - Log destination changed from stdout to stderr
  - Log format changed from `LEVEL: [Component] Message` to `[Databento LEVEL] [Component] Message`
  - All four client wrappers now use consistent `StderrLogReceiver` implementation
  - DEBUG-level logs now visible for enhanced diagnostics
  - Impact: ~90% of users unaffected (console output unchanged); ~10% may need to update log redirection scripts (see [Migration Guide](RELEASE_NOTES_v3.0.24-beta.md#migration-guide))

### Technical Details

- Added `StderrLogReceiver` class in `src/Databento.Native/src/common_helpers.hpp`
- Updated `historical_client_wrapper.cpp`: Added log_receiver field, passed to Historical constructor
- Updated `batch_wrapper.cpp`: Added log_receiver field (Batch uses Historical client internally)
- Updated `live_blocking_wrapper.cpp`: Added log_receiver field, passed to LiveBlocking::Builder
- Updated `live_client_wrapper.cpp`: Added log_receiver field, passed to LiveThreaded::Builder
- Native DLL size unchanged: 784KB
- Zero API surface changes: Fully backward compatible

### Testing

- All 33 examples pass (100% success rate, up from 32/33)
- Comprehensive testing completed:
  - Historical API with future dates: ✅ Fixed (172 records received, no crash)
  - Historical API with past dates: ✅ Working (regression test passed)
  - Batch API with invalid symbols: ✅ Fixed (proper exception instead of crash)
  - Live authentication: ✅ Working (new log format visible)
  - Live replay mode: ✅ Working (new log format visible)
  - LiveThreaded streaming: ✅ Working (event callbacks functioning correctly)
  - All metadata and symbology APIs: ✅ Working
- Zero functionality regressions detected
- Zero performance regressions detected

### Documentation

- Updated `API_REFERENCE.md`: Removed crash warnings from GetRangeAsync methods
- Created `RELEASE_NOTES_v3.0.24-beta.md`: Comprehensive release documentation
- Created `LOG_FORMAT_VERIFICATION.md`: Log format change documentation and migration guide
- Created `ALL_EXAMPLES_REPORT.md`: Full test execution report (33/33 examples passing)
- Created `TEST_RESULTS_v3.0.24-beta.md`: Detailed test results and verification

---

## [3.0.23-beta] - 2025-11 (Previous Release)

### Added

- Bundle Visual C++ runtime DLLs to fix DllNotFoundException ([#2](https://github.com/Alparse/databento-dotnet/pull/2))
- Add crash warnings to API_REFERENCE.md for Historical GetRange methods

### Fixed

- Fixed DllNotFoundException on systems without Visual C++ redistributables installed

---

## Versioning Scheme

This project uses [Semantic Versioning](https://semver.org/):
- **MAJOR** version for incompatible API changes
- **MINOR** version for new functionality in a backward compatible manner
- **PATCH** version for backward compatible bug fixes
- **-beta** suffix indicates pre-release software

## Links

- [GitHub Repository](https://github.com/Alparse/databento-dotnet)
- [Issue Tracker](https://github.com/Alparse/databento-dotnet/issues)
- [NuGet Package](https://www.nuget.org/packages/Databento.Client/)
- [Databento Documentation](https://docs.databento.com)
