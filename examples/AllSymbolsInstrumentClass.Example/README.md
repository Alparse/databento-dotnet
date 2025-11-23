# ALL_SYMBOLS InstrumentClass Test

## Overview

This example demonstrates querying all instruments in a dataset using `ALL_SYMBOLS` and verifying that `InstrumentClass` is correctly populated in v4.0.0.

## What It Tests

- **ALL_SYMBOLS query**: Gets all instruments in the dataset (not just specific symbols)
- **InstrumentClass population**: Verifies that InstrumentClass is correctly set for each instrument
- **Distribution analysis**: Shows breakdown of instrument types across the entire dataset

## The Fix (v4.0.0)

In v3.x, `InstrumentClass` was **always 0** (Unknown) due to reading from the wrong byte offset (319 instead of 487).

In v4.0.0, `InstrumentClass` is now correctly populated with values like:
- `Future` - Futures contracts
- `Call` - Call options
- `Put` - Put options
- `Stock` - Equity instruments
- `FutureSpread` - Calendar spreads, butterflies, etc.
- `OptionSpread` - Option combo strategies
- And more...

## How to Run

### Prerequisites

1. **Databento API Key**: Set environment variable
   ```bash
   # Windows
   setx DATABENTO_API_KEY "your-api-key-here"

   # Linux/Mac
   export DATABENTO_API_KEY="your-api-key-here"
   ```

2. **Data Access**: Requires access to a dataset with Definition schema (e.g., GLBX.MDP3)

### Running the Example

```bash
# From repository root
cd examples/AllSymbolsInstrumentClass.Example
dotnet run

# Or from repository root
dotnet run --project examples/AllSymbolsInstrumentClass.Example
```

## Expected Output

```
================================================================================
ALL_SYMBOLS InstrumentClass Test (v4.0.0)
================================================================================

✓ Created HistoricalClient

Query Parameters:
  Dataset:      GLBX.MDP3
  Symbols:      ALL_SYMBOLS
  Schema:       Definition
  Start Time:   2023-11-14 00:00:00
  End Time:     2023-11-15 00:00:00
  Record Limit: 100

Querying instrument definitions for ALL_SYMBOLS...

  [  1] ESZ3                      InstrumentClass: Future
  [  2] ESZ3                      InstrumentClass: Future
  [  3] ESZ3_W1_4750C             InstrumentClass: Call
  [  4] ESZ3_W1_4750P             InstrumentClass: Put
  [  5] ESZ3_W1_4775C             InstrumentClass: Call
  [  6] ESZ3_W1_4775P             InstrumentClass: Put
  [  7] GCZ3                      InstrumentClass: Future
  [  8] GCZ3                      InstrumentClass: Future
  [  9] NQZ3                      InstrumentClass: Future
  [ 10] NQZ3                      InstrumentClass: Future
  [ 11] ESZ3:ESH4                 InstrumentClass: FutureSpread
  [ 12] ESZ3:ESH4:ESM4            InstrumentClass: FutureSpread
  ...
  [ 20] CLZ3                      InstrumentClass: Future
  ... (showing first 20, continuing to collect data)

================================================================================
RESULTS
================================================================================

Total Instruments Processed: 100

InstrumentClass Distribution:
  Future                 45 instruments (45.0%)
  Call                   20 instruments (20.0%)
  Put                    20 instruments (20.0%)
  FutureSpread           12 instruments (12.0%)
  OptionSpread            3 instruments (3.0%)

✓ SUCCESS: v4.0.0 fix is working!
  100 instruments have valid InstrumentClass values
  0 instruments are Unknown (this is normal for undefined types)
```

## What Success Looks Like

When the test **passes**, you should see:
- ✅ Multiple different `InstrumentClass` values (Future, Call, Put, etc.)
- ✅ A distribution showing variety of instrument types
- ✅ Green "SUCCESS" message confirming the fix works

## What Failure Looks Like (v3.x Behavior)

If you were running this on v3.x, you'd see:
- ❌ InstrumentClass: Unknown for every single instrument
- ❌ Distribution showing 100% Unknown
- ❌ Red "FAILURE" message

## Customization

You can modify the query parameters in `Program.cs`:

```csharp
// Try different datasets
string dataset = "XNAS.ITCH";  // NASDAQ equities

// Adjust date range
var endTime = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);
var startTime = endTime.AddDays(-7);  // 7 days of data

// Get more records
int recordLimit = 500;

// Change display limit
const int displayLimit = 50;  // Show first 50 instruments
```

## Use Cases

This example is useful for:

1. **Dataset exploration**: See what instrument types exist in a dataset
2. **Data validation**: Verify that instrument definitions are being parsed correctly
3. **Migration testing**: Confirm v4.0.0 fix works across all instruments
4. **Analytics**: Understand composition of a dataset (% futures, % options, etc.)

## Performance Notes

- **Record Limit**: Default is 100 to keep runtime short. Increase for more comprehensive analysis.
- **Date Range**: Shorter date ranges (1 day) are faster. Longer ranges may take several minutes.
- **Display Limit**: Only first 20 instruments are printed to avoid console spam, but all are counted.

## Troubleshooting

### No instruments returned

**Symptom:**
```
Total Instruments Processed: 0
```

**Causes:**
1. No definition updates in the date range
2. API key lacks access to Definition schema
3. Dataset doesn't support Definition schema

**Solutions:**
- Try a different date range
- Check your Databento account permissions
- Verify dataset supports Definition schema: https://databento.com/docs/schemas-and-data-formats/whats-a-schema

### All instruments are Unknown

**Symptom:**
```
✗ FAILURE: All instruments have InstrumentClass = Unknown
```

**Cause:**
This indicates the v4.0.0 fix isn't working.

**Solutions:**
1. Verify you're running v4.0.0-beta: `dotnet list package`
2. Clean and rebuild: `dotnet clean && dotnet build`
3. Check package restore: `dotnet restore`

## Related

- **Issue**: [#4 InstrumentDefMessage.InstrumentClass is always 0](https://github.com/Alparse/databento-dotnet/issues/4)
- **Comprehensive Test**: `examples/InstrumentDefTest.v4/` - Tests all v4.0.0 changes
- **Migration Guide**: `MIGRATION_GUIDE_v4.0.0.md`
- **CHANGELOG**: `CHANGELOG.md` (v4.0.0-beta entry)

## Technical Details

### Query Pattern

```csharp
await foreach (var record in client.GetRangeAsync(
    dataset: "GLBX.MDP3",
    schema: Schema.Definition,
    symbols: new[] { "ALL_SYMBOLS" },  // Gets all instruments
    startTime: start,
    endTime: end))
{
    if (record is InstrumentDefMessage def)
    {
        Console.WriteLine(def.InstrumentClass);  // Now correctly populated!
    }
}
```

### Key Difference from v3.x

**v3.x (BROKEN)**:
- `InstrumentClass` field read from offset 319
- Offset 319 contains random data
- Result: Always returned 0 (Unknown)

**v4.0.0 (FIXED)**:
- `InstrumentClass` field read from offset 487 (correct per DBN v2 spec)
- Offset 487 contains the actual instrument class byte
- Result: Returns correct values (Future, Call, Put, Stock, etc.)

## Support

If this test fails or you encounter issues:
1. Check the output for specific error messages
2. Review the [Migration Guide](../../MIGRATION_GUIDE_v4.0.0.md)
3. Open an issue on GitHub with the full output
