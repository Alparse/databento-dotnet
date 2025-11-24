# Instrument Definition Decoder Example

This example demonstrates how to decode instrument definition records (`InstrumentDefMessage`) from the Historical API.

## What This Example Shows

- How to query instrument definitions using `Schema.Definition`
- How to decode `InstrumentDefMessage` records
- How to access fields like `InstrumentClass`, `RawSymbol`, `Asset`, `Exchange`, `Currency`
- How to work with strike prices and display factors

## Issue #4 Fix Demonstration

This example specifically demonstrates the fix for [Issue #4](https://github.com/Alparse/databento-dotnet/issues/4):

**Before Fix (v3.0.26-beta and earlier):**
- `InstrumentClass` was always `0` (Unknown)
- All instrument definitions showed incorrect class

**After Fix (v3.0.27-beta and later):**
- `InstrumentClass` correctly populated with values like:
  - `Future` - Futures contracts
  - `Call` - Call options
  - `Put` - Put options
  - `Stock` - Equities
  - `FutureSpread` - Futures spreads
  - `OptionSpread` - Option spreads
  - And more...

## Running the Example

```bash
# Set your API key
export DATABENTO_API_KEY="your-api-key"

# Run the example
dotnet run --configuration Release
```

## Expected Output

```
================================================================================
Instrument Definition Decoder Example
================================================================================

✓ Created HistoricalClient

Query Parameters:
  Dataset:  GLBX.MDP3
  Symbols:  ALL_SYMBOLS (all instruments)
  Schema:   Definition
  Date:     2023-11-14
  Limit:    First 20 records

Decoding instrument definitions...

[  1] LNEV6 C12500              Class: Call            Asset: LN
     └─ Exchange: XCME, Currency: USD
     └─ Strike Price: 125.00
[  2] OMGG4 C2085               Class: Call            Asset: OM
     └─ Exchange: XCME, Currency: USD
     └─ Strike Price: 20.85
[  3] OZSH4 C1520               Class: Call            Asset: OZ
     └─ Exchange: XCME, Currency: USD
     └─ Strike Price: 15.20
[  4] LOX4 P250                 Class: Put             Asset: LO
     └─ Exchange: XCME, Currency: USD
     └─ Strike Price: 2.50
[  5] OHV4 P24900               Class: Put             Asset: OH
     └─ Exchange: XCME, Currency: USD
     └─ Strike Price: 249.00
[  6] AHMG4                     Class: Future          Asset: AHM
[  7] OBK4 C29300               Class: Call            Asset: OB
...

================================================================================
SUMMARY
================================================================================
Total instrument definitions decoded: 1234

InstrumentClass field is now correctly populated!
This demonstrates Issue #4 fix - InstrumentClass was always 0, now shows:
  - Future, Call, Put, Stock, FutureSpread, OptionSpread, etc.
```

## Key Concepts

### InstrumentDefMessage Fields

```csharp
if (record is InstrumentDefMessage def)
{
    // Basic identification
    Console.WriteLine($"Symbol: {def.RawSymbol}");
    Console.WriteLine($"Class: {def.InstrumentClass}");
    Console.WriteLine($"Asset: {def.Asset}");
    Console.WriteLine($"Exchange: {def.Exchange}");

    // Pricing information
    Console.WriteLine($"Currency: {def.Currency}");
    Console.WriteLine($"Min Price Increment: {def.MinPriceIncrement}");
    Console.WriteLine($"Display Factor: {def.DisplayFactor}");

    // Options-specific
    if (def.StrikePrice != 0)
    {
        var strike = def.StrikePrice / (double)def.DisplayFactor;
        Console.WriteLine($"Strike: ${strike:F2}");
    }

    // Expiration
    if (def.Expiration != 0)
    {
        var expiry = DateTimeOffset.FromUnixTimeNanoseconds(def.Expiration);
        Console.WriteLine($"Expires: {expiry:yyyy-MM-dd}");
    }
}
```

### Filtering by Instrument Class

```csharp
await foreach (var record in client.GetRangeAsync(...))
{
    if (record is InstrumentDefMessage def)
    {
        // Only process futures
        if (def.InstrumentClass == InstrumentClass.Future)
        {
            Console.WriteLine($"Future: {def.RawSymbol}");
        }

        // Only process options (calls and puts)
        if (def.InstrumentClass == InstrumentClass.Call ||
            def.InstrumentClass == InstrumentClass.Put)
        {
            Console.WriteLine($"Option: {def.RawSymbol}");
        }
    }
}
```

## Related Examples

- **AllSymbolsInstrumentClass.Example** - Shows distribution of instrument classes
- **InstrumentDefTest.v4** - Comprehensive test of all InstrumentDefMessage fields

## API Reference

- [Databento Schema Documentation](https://databento.com/docs/schemas-and-data-formats/instrument-definitions)
- [Historical API Reference](https://databento.com/docs/api-reference-historical)
