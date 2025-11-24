# Session Update: v3.0.27-beta & v3.0.28-beta Releases - Critical Bug Fixes

**Date**: November 23, 2025
**Versions Released**: 3.0.27-beta, 3.0.28-beta
**Status**: ✅ **BOTH VERSIONS DEPLOYED TO PRODUCTION**

---

## Executive Summary

This session completed the implementation, testing, and deployment of fixes for two critical bugs:

### Issue #1 (Revisited): AccessViolationException with Future Dates
- **Root Cause**: NULL ILogReceiver pointer in native wrapper
- **Solution**: Created StderrLogReceiver class, updated all wrappers to pass valid pointer
- **Files Modified**: `common_helpers.hpp`, `historical_client_wrapper.cpp`, `batch_wrapper.cpp`, `live_blocking_wrapper.cpp`
- **Status**: ✅ FIXED and VERIFIED

### Issue #4: InstrumentDefMessage.InstrumentClass Always 0
- **Root Cause**: Incorrect field offsets in deserialization code
- **Solution**: Corrected all offsets to match DBN v2 spec, added 13 missing fields
- **Files Modified**: `InstrumentDefMessage.cs`, `Record.cs:416-535`
- **Status**: ✅ FIXED and VERIFIED

### Two Releases
1. **v3.0.27-beta**: Initial release with both fixes
2. **v3.0.28-beta**: Maintenance release with clean, unmodified databento-cpp v0.44.0 (RECOMMENDED)

---

## v3.0.27-beta Release Details

**Published**: November 23, 2025

### Changes
- Fixed Issue #1: NULL ILogReceiver causing AccessViolationException
- Fixed Issue #4: InstrumentDefMessage field offsets and missing fields
- Updated version to 3.0.27-beta
- Updated README badges (version v3.0.27-beta, downloads 3.5K)
- Updated CHANGELOG with comprehensive entry

### Testing
**Issue #1 Test** (HistoricalFutureDates.Test):
- ✅ 172 records received
- ✅ Server warning displayed on stderr
- ✅ No crash

**Issue #4 Test** (AllSymbolsInstrumentClass.Example):
- ✅ All instrument classes correctly identified
- ✅ Distribution: Call (48%), Put (32%), FutureSpread (10%), Future (6%), OptionSpread (3%)
- ✅ NO Unknown values (was 100% before fix)

### Git & NuGet
- Commit: `891a8f5`
- Pushed to both remotes (origin & public)
- Published to NuGet.org: https://www.nuget.org/packages/Databento.Client/3.0.27-beta

---

## Critical Discovery: Debug-Modified databento-cpp

After publishing v3.0.27-beta, discovered that the native DLL was built with a databento-cpp version that had been modified during debugging sessions.

**Action Taken**: Complete clean rebuild

### Clean Rebuild Process
1. Deleted entire `src/Databento.Native/build/` directory
2. Reconfigured CMake with vcpkg toolchain
3. CMake fetched fresh databento-cpp v0.44.0 from official GitHub
4. Built in Release mode
5. DLL automatically copied to runtime location

**Verification**:
- databento-cpp version: v0.44.0 (Release)
- Git commit: `3de8e70`
- Source: Official GitHub repository (clean, unmodified)
- Build timestamp: Nov 23 01:31
- DLL size: 791 KB

**Re-tested Both Fixes**:
- ✅ HistoricalFutureDates.Test: PASS (172 records, warning displayed, no crash)
- ✅ AllSymbolsInstrumentClass.Example: PASS (all classes correct)

---

## v3.0.28-beta Release Details (RECOMMENDED)

**Published**: November 23, 2025

### Purpose
Maintenance release to replace v3.0.27-beta's native DLL with one built from guaranteed clean, official databento-cpp v0.44.0.

**Key Point**: All functionality identical to v3.0.27-beta. Only difference is clean dependency provenance.

### Changes
- Native DLL rebuilt with clean databento-cpp v0.44.0
- Updated version to 3.0.28-beta
- Updated README badge to v3.0.28-beta
- Updated CHANGELOG with maintenance release explanation

### Testing
Both fixes re-verified with clean build:
- ✅ HistoricalFutureDates.Test: PASS
- ✅ AllSymbolsInstrumentClass.Example: PASS

### Git & NuGet
- Commit: `7d8dc74`
- Pushed to both remotes (origin & public)
- Published to NuGet.org: https://www.nuget.org/packages/Databento.Client/3.0.28-beta

---

## New Example Project: InstrumentDefinitionDecoder.Example

Created educational example demonstrating:
- How to query instrument definitions (Schema.Definition)
- How to decode InstrumentDefMessage records
- How to access InstrumentClass and other fields
- Demonstrates Issue #4 fix in action

**Files**:
- `examples/InstrumentDefinitionDecoder.Example/InstrumentDefinitionDecoder.Example.csproj`
- `examples/InstrumentDefinitionDecoder.Example/Program.cs`
- `examples/InstrumentDefinitionDecoder.Example/README.md`

**Test Results**: Successfully decoded 653,411 instrument definitions
- First 20 shown with detailed info
- All instrument classes correctly displayed
- Added to solution with `dotnet sln add`

---

## Files Modified This Session

### Version Updates
1. `src/Databento.Client/Databento.Client.csproj` - v3.0.27-beta → v3.0.28-beta
2. `src/Databento.Interop/Databento.Interop.csproj` - v3.0.27-beta → v3.0.28-beta

### Documentation Updates
1. `README.md` - Updated badges (v3.0.28-beta, 3.5K downloads)
2. `CHANGELOG.md` - Added v3.0.27-beta and v3.0.28-beta entries

### Native Library
1. `src/Databento.Interop/runtimes/win-x64/native/databento_native.dll` - Clean rebuild

### New Files
1. `examples/InstrumentDefinitionDecoder.Example/` - Complete example project
2. `SESSION_NOTES_NOV23.md` - This file (to be appended to session_status.md)

---

## Git Activity

**Commit 1**: `891a8f5` - "chore: Release v3.0.27-beta - Critical bug fixes"
**Commit 2**: `7d8dc74` - "chore: Release v3.0.28-beta - Clean databento-cpp rebuild"

Both commits pushed to:
- origin: https://github.com/Alparse/databento_client.git
- public: https://github.com/Alparse/databento-dotnet.git

---

## NuGet.org Publication

**v3.0.27-beta**:
- Main: Databento.Client.3.0.27-beta.nupkg (4.0 MB)
- Symbols: Databento.Client.3.0.27-beta.snupkg (40 KB)
- URL: https://www.nuget.org/packages/Databento.Client/3.0.27-beta

**v3.0.28-beta** (RECOMMENDED):
- Main: Databento.Client.3.0.28-beta.nupkg (4.0 MB)
- Symbols: Databento.Client.3.0.28-beta.snupkg (40 KB)
- URL: https://www.nuget.org/packages/Databento.Client/3.0.28-beta

---

## Comparison: v3.0.27-beta vs v3.0.28-beta

| Aspect | v3.0.27-beta | v3.0.28-beta |
|--------|--------------|--------------|
| Issue #1 Fix | ✅ Included | ✅ Included (identical) |
| Issue #4 Fix | ✅ Included | ✅ Included (identical) |
| C# Code | Identical | Identical |
| Native DLL | Nov 20 build | Nov 23 clean rebuild |
| databento-cpp | Unknown provenance | Official v0.44.0 |
| Recommendation | OK if installed | **RECOMMENDED** |

---

## Status of All Outstanding Issues

### Issue #1 (Original): Invalid Symbols
**Status**: ⚠️ DOCUMENTED (awaiting upstream fix)
- Workarounds: Pre-validate symbols, use Batch API, use Live API

### Issue #1 (Revisited): Future Dates AccessViolationException
**Status**: ✅ FIXED in v3.0.27-beta and v3.0.28-beta

### Issue #2: Missing VC++ Runtime DLLs
**Status**: ✅ RESOLVED in v3.0.23-beta and all subsequent versions

### Issue #4: InstrumentClass Always 0
**Status**: ✅ FIXED in v3.0.27-beta and v3.0.28-beta

---

## Key Decisions

1. **Non-Breaking Release**: Released as v3.0.27-beta (patch) not v4.0.0
   - Justification: Fixes restore correct behavior per specification

2. **Maintenance Release**: Created v3.0.28-beta for clean dependencies
   - Justification: Clear versioning, production confidence

3. **Static Badges**: Used static version/download badges
   - Justification: Reliable display, manual control

4. **Clean Rebuild**: Deleted entire build/ directory
   - Justification: Only way to guarantee clean dependencies

---

## Session Timeline

- **00:00** - Session start, read session_status.md
- **00:30** - Validated Issue #1 and Issue #4 fixes
- **01:00** - Updated version, README, CHANGELOG for v3.0.27-beta
- **01:30** - Built, packed, tested
- **02:00** - Committed, pushed to git, published to NuGet
- **02:30** - Discovered debug-modified databento-cpp
- **02:45** - Clean rebuild with official databento-cpp
- **03:00** - Re-tested fixes
- **03:15** - Updated to v3.0.28-beta, built, packed
- **03:30** - Committed, pushed, published v3.0.28-beta
- **03:45** - Created InstrumentDefinitionDecoder.Example
- **04:00** - Documented session in session_status.md

---

## Statistics

**Releases**: 2 (v3.0.27-beta, v3.0.28-beta)
**NuGet Packages**: 4 total (2 .nupkg + 2 .snupkg)
**Git Commits**: 2
**Test Projects Run**: 3
**Records Processed**: ~654,000
**Build Success Rate**: 100%

---

## Production Readiness: ✅ EXCELLENT

- Code Quality: ✅ Two critical bugs fixed
- Testing: ✅ Comprehensive manual testing
- Documentation: ✅ CHANGELOG, README, examples all updated
- Dependencies: ✅ Clean, official databento-cpp v0.44.0
- Deployment: ✅ Git and NuGet.org published

**Recommendation**: v3.0.28-beta is production-ready and recommended for all users.

---

**Session Completed**: November 23, 2025
**Duration**: ~4 hours
**Status**: ✅ ALL OBJECTIVES ACHIEVED
