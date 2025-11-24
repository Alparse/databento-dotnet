# URGENT SESSION NOTES - Nov 23, 2025

## CRITICAL: TWO VERSIONS RELEASED TODAY

### v3.0.27-beta - First Release
- **Fixed Issue #1**: NULL ILogReceiver causing AccessViolationException with future dates
  - Created StderrLogReceiver class in common_helpers.hpp:261-277
  - Updated all wrappers: historical_client_wrapper.cpp, batch_wrapper.cpp, live_blocking_wrapper.cpp
  - Test: HistoricalFutureDates.Test - 172 records, no crash ✅

- **Fixed Issue #4**: InstrumentDefMessage.InstrumentClass always 0
  - Corrected field offsets in Record.cs:416-535
  - InstrumentClass now reads from offset 487 (was 319)
  - StrikePrice now reads from offset 104 (was 320)
  - Added 13 missing multi-leg strategy fields
  - Test: AllSymbolsInstrumentClass.Example - All classes correct ✅

- **Published**: Git commit 891a8f5, NuGet.org, both remotes (origin & public)

### v3.0.28-beta - Clean Rebuild (RECOMMENDED)
- **Why**: v3.0.27-beta had native DLL built with potentially debug-modified databento-cpp
- **Action**: Deleted entire build/, rebuilt from scratch with official databento-cpp v0.44.0
- **Verification**: Both fixes re-tested and working ✅
- **Published**: Git commit 7d8dc74, NuGet.org, both remotes

## NEW EXAMPLE PROJECT
- **InstrumentDefinitionDecoder.Example** created
- Demonstrates Issue #4 fix
- Decoded 653,411 instruments successfully
- Added to solution

## KEY FILES MODIFIED
1. src/Databento.Client/Databento.Client.csproj - v3.0.28-beta
2. src/Databento.Interop/Databento.Interop.csproj - v3.0.28-beta
3. README.md - badges updated (v3.0.28-beta, 3.5K downloads)
4. CHANGELOG.md - added v3.0.27-beta and v3.0.28-beta entries
5. databento_native.dll - clean rebuild with official databento-cpp v0.44.0
6. examples/InstrumentDefinitionDecoder.Example/ - NEW

## NATIVE LIBRARY DETAILS
- **databento-cpp version**: v0.44.0 (official from GitHub)
- **Commit**: 3de8e70
- **Built**: Nov 23 01:31
- **Size**: 791 KB
- **Clean**: Yes, guaranteed unmodified official release

## TESTING SUMMARY
All tests PASS:
- HistoricalFutureDates.Test - Issue #1 fix ✅
- AllSymbolsInstrumentClass.Example - Issue #4 fix ✅
- InstrumentDefinitionDecoder.Example - New example ✅
- Full solution build - 0 errors ✅

## GIT STATUS
- Commit 1: 891a8f5 (v3.0.27-beta)
- Commit 2: 7d8dc74 (v3.0.28-beta)
- Pushed to: origin (databento_client.git) AND public (databento-dotnet.git)

## NUGET STATUS
Both versions published:
- v3.0.27-beta: https://www.nuget.org/packages/Databento.Client/3.0.27-beta
- v3.0.28-beta: https://www.nuget.org/packages/Databento.Client/3.0.28-beta (RECOMMENDED)

## ISSUE STATUS
- Issue #1 (future dates): ✅ FIXED
- Issue #4 (InstrumentClass): ✅ FIXED
- Issue #2 (VC++ DLLs): ✅ Already fixed in v3.0.23-beta

## PRODUCTION STATUS
✅ v3.0.28-beta is PRODUCTION READY
✅ Both critical bugs fixed and verified
✅ Clean dependencies (official databento-cpp v0.44.0)
✅ All tests passing

## RECOMMENDATION
**Use v3.0.28-beta** - has clean, official dependencies
v3.0.27-beta works but may have had debug-modified databento-cpp

---
Session Duration: ~4 hours
Status: ✅ ALL OBJECTIVES ACHIEVED
