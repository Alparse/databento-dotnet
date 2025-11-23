# InstrumentDefTest.v4 - Comprehensive v4.0.0 Test Program

## Overview

This example program comprehensively tests and demonstrates the v4.0.0 fix for `InstrumentDefMessage` deserialization (Issue #4).

## What It Tests

### ✅ Test 1: InstrumentClass Populated
- **Issue**: In v3.x, `InstrumentClass` was always `0` (Unknown)
- **Fix**: Now reads from correct offset 487
- **Verification**: Shows distribution of instrument classes and confirms values are populated

### ✅ Test 2: Detailed Field Inspection
- Displays comprehensive field information for each instrument type
- Verifies all fields are reading correct data from correct offsets

### ✅ Test 3: String Field Verification
- Confirms string fields have correct lengths and content
- **Key fixes**:
  - `RawSymbol`: Now 71 bytes (was 22 bytes)
  - `Asset`: Now 11 bytes (was 7 bytes)
  - All strings reading from correct offsets per DBN v2 spec

### ✅ Test 4: Multi-Leg Strategy Fields
- Tests 13 new fields added in v4.0.0
- Detects spreads and combo instruments
- Displays leg information (ratios, prices, deltas, symbols)

### ✅ Test 5: Filtering by InstrumentClass
- **Broken in v3.x**: Filtering returned empty results
- **Fixed in v4.0.0**: Can now filter futures, options, spreads correctly
- Demonstrates practical filtering examples

### ✅ Test 6: RawInstrumentId Type
- Verifies type changed from `uint` to `ulong` (correct per DBN spec)

### ✅ Test 7: Removed Fields
- Documents removal of obsolete fields:
  - `TradingReferencePrice` (not in DBN spec)
  - `TradingReferenceDate` (not in DBN spec)

## How to Run

### Prerequisites

1. **Databento API Key**: Set environment variable
   ```bash
   # Windows
   setx DATABENTO_API_KEY "your-api-key-here"

   # Linux/Mac
   export DATABENTO_API_KEY="your-api-key-here"
   ```

2. **Data Access**: Requires access to `GLBX.MDP3` dataset with Definition schema

### Running the Test

```bash
# From repository root
cd examples/InstrumentDefTest.v4
dotnet run

# Or from repository root
dotnet run --project examples/InstrumentDefTest.v4
```

### Expected Output

```
================================================================================
InstrumentDefMessage v4.0.0 Comprehensive Test
================================================================================

✓ Created HistoricalClient

Test Parameters:
  Dataset:      GLBX.MDP3
  Symbols:      ESZ3, GCZ3, NQZ3
  Start Time:   2023-11-08
  End Time:     2023-11-15
  Record Limit: 50

Querying instrument definitions...

✓ Received 18 instrument definitions

================================================================================
TEST 1: InstrumentClass Populated (Issue #4 Fix)
================================================================================

InstrumentClass Distribution:
  Future                18 records (100.0%)

✓ SUCCESS: 18 instruments have valid InstrumentClass values!

...
[Additional test output]
...

================================================================================
SUMMARY
================================================================================

Total Records Tested:         18
InstrumentClass Populated:    18 (100.0%)
InstrumentClass Unknown:      0
Multi-Leg Instruments:        0
Unique Instrument Classes:    1

═══════════════════════════════════════════════════════════════════════════════
✓ ALL TESTS PASSED
═══════════════════════════════════════════════════════════════════════════════

v4.0.0 InstrumentDefMessage fix is working correctly!
  ✓ InstrumentClass is now populated with correct values
  ✓ All string fields are reading from correct offsets
  ✓ New multi-leg fields are available
  ✓ Filtering by InstrumentClass now works
  ✓ RawInstrumentId correctly typed as ulong
```

## What Success Looks Like

When the test **passes**, you should see:
- ✅ InstrumentClass values distributed across Future, Call, Put, Stock, etc. (NOT all Unknown/0)
- ✅ String fields properly populated with correct lengths
- ✅ Filtering by InstrumentClass returns results
- ✅ Multi-leg fields available (if spreads present in data)
- ✅ Final verdict: "ALL TESTS PASSED"

## What Failure Looks Like (v3.x Behavior)

If you were running this on v3.x, you'd see:
- ❌ InstrumentClass: 100% Unknown (all values = 0)
- ❌ String fields truncated or wrong
- ❌ Filtering by InstrumentClass returns empty results
- ❌ Multi-leg fields all zero/empty
- ❌ Final verdict: "SOME TESTS FAILED"

## Troubleshooting

### No instrument definitions returned

**Symptom:**
```
⚠ No instrument definitions returned.
```

**Causes:**
1. No definition updates in the date range (try wider range)
2. Symbols don't exist in dataset
3. API key lacks access to Definition schema

**Solutions:**
- Try a wider date range (e.g., `-30` days)
- Try different symbols (ES.FUT, GC.FUT are common)
- Check your Databento account permissions

### All InstrumentClass values are Unknown

**Symptom:**
```
✗ FAILURE: All instruments have InstrumentClass = Unknown
```

**Cause:**
This indicates the v4.0.0 fix isn't working.

**Solutions:**
1. Verify you're running v4.0.0-beta: `dotnet list package`
2. Clean and rebuild: `dotnet clean && dotnet build`
3. Check the native DLL version matches

## Customization

You can modify the test parameters in `Program.cs`:

```csharp
// Test different datasets
string dataset = "XNAS.ITCH";  // Try NASDAQ

// Test different symbols (use specific contract codes for futures)
string[] symbols = { "ESH5", "GCZ4", "NQU5" };  // Specific futures contracts
// Or for equities:
// string[] symbols = { "AAPL", "MSFT", "TSLA" };

// Test different date ranges
var endTime = new DateTimeOffset(2023, 12, 15, 0, 0, 0, TimeSpan.Zero);
var startTime = endTime.AddDays(-30);  // 30 days back

// Test more records
int recordLimit = 100;  // Fetch 100 records
```

## Integration with CI/CD

This example can be used as an integration test in CI/CD pipelines:

```yaml
# GitHub Actions example
- name: Run InstrumentDefMessage Test
  run: dotnet run --project examples/InstrumentDefTest.v4
  env:
    DATABENTO_API_KEY: ${{ secrets.DATABENTO_API_KEY }}
```

Exit codes:
- `0`: All tests passed
- Non-zero: Tests failed or error occurred

## Related Files

- **Issue**: [#4 InstrumentDefMessage.InstrumentClass is always 0](https://github.com/Alparse/databento-dotnet/issues/4)
- **Fix Plan**: `ISSUE_4_FIX_PLAN_REVISED.md`
- **Migration Guide**: `MIGRATION_GUIDE_v4.0.0.md`
- **CHANGELOG**: `CHANGELOG.md` (v4.0.0-beta entry)

## Technical Details

### DBN v2 Specification References
- **Official Spec**: https://docs.rs/dbn/latest/dbn/record/struct.InstrumentDefMsg.html
- **Struct Size**: 520 bytes (verified)
- **InstrumentClass Offset**: 487 (fixed from 319)
- **StrikePrice Offset**: 104 (fixed from 320)

### Field Offset Corrections
The test verifies these critical fixes:
- **String fields**: All moved to correct offsets (224-486)
- **Enum fields**: `InstrumentClass` at 487, `MatchAlgorithm` at 488
- **Multi-leg fields**: 13 new fields added (offsets 88-502)

## Support

If this test fails or you encounter issues:
1. Check the output for specific failure details
2. Review the [Migration Guide](../../MIGRATION_GUIDE_v4.0.0.md)
3. Open an issue on GitHub with the full output
