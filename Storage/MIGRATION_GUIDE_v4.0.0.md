# Migration Guide: v3.x → v4.0.0-beta

This guide helps you migrate from databento-dotnet v3.x to v4.0.0-beta.

## Overview

Version 4.0.0-beta contains **breaking changes** to `InstrumentDefMessage` deserialization. All field offsets have been corrected to match the official DBN v2 specification, meaning field values will be different from v3.x.

## Who Needs to Migrate?

You are affected if your application:
- Queries instrument definitions using `schema=Definition` on any dataset
- Uses any fields from `InstrumentDefMessage` for filtering, analysis, or display
- Caches or persists instrument definition data
- Uses `RawInstrumentId` for comparisons

You are **NOT affected** if your application:
- Only uses trade data (MBO, MBP, OHLCV schemas)
- Only uses live streaming without instrument definitions
- Doesn't use `InstrumentDefMessage` at all

## Breaking Changes Summary

### 1. Field Values Changed (ALL FIELDS)

**Impact**: HIGH
**Reason**: Byte offsets corrected to match DBN v2 specification

All `InstrumentDefMessage` fields now return **correct values** according to the DBN specification. Previously, fields were reading from wrong offsets, returning incorrect data.

**Example of what changes:**

```csharp
// v3.x - INCORRECT VALUES
var def = await client.GetRangeAsync(..., schema: Schema.Definition).FirstAsync();
Console.WriteLine(def.InstrumentClass);  // Always printed: Unknown (0)
Console.WriteLine(def.RawSymbol);        // May be truncated or wrong
Console.WriteLine(def.Asset);            // May be truncated or wrong

// v4.0.0 - CORRECT VALUES
var def = await client.GetRangeAsync(..., schema: Schema.Definition).FirstAsync();
Console.WriteLine(def.InstrumentClass);  // Prints: Future, Call, Put, Stock, etc.
Console.WriteLine(def.RawSymbol);        // Correct full symbol (up to 71 chars)
Console.WriteLine(def.Asset);            // Correct asset (up to 11 chars)
```

### 2. InstrumentClass Now Correctly Populated

**Impact**: HIGH
**Reason**: Was reading from offset 319, now reads from correct offset 487

**Before (v3.x):**
```csharp
// ALL instruments returned InstrumentClass = 0 (now mapped to Unknown)
if (def.InstrumentClass == 0)
{
    // This matched EVERYTHING in v3.x
}
```

**After (v4.0.0):**
```csharp
// Instruments now return correct class
if (def.InstrumentClass == InstrumentClass.Unknown)
{
    // This only matches truly undefined instruments
}

// Filter for futures
if (def.InstrumentClass == InstrumentClass.Future)
{
    // This NOW WORKS correctly
}
```

**Migration Action:**
- Remove any code that assumes `InstrumentClass == 0` for all instruments
- Update filters to use proper enum values (`Future`, `Call`, `Put`, `Stock`, etc.)
- Add explicit handling for `InstrumentClass.Unknown` if needed

### 3. RawInstrumentId Type Changed: uint → ulong

**Impact**: MEDIUM
**Reason**: DBN specification defines this as 64-bit (ulong)

**Before (v3.x):**
```csharp
uint rawId = def.RawInstrumentId;  // ✅ Compiled
Dictionary<uint, string> cache = new();
cache[def.RawInstrumentId] = def.RawSymbol;
```

**After (v4.0.0):**
```csharp
ulong rawId = def.RawInstrumentId;  // ✅ Correct type
Dictionary<ulong, string> cache = new();  // Update dictionary type
cache[def.RawInstrumentId] = def.RawSymbol;
```

**Migration Action:**
- Change all `uint` variables holding `RawInstrumentId` to `ulong`
- Update dictionary keys from `Dictionary<uint, ...>` to `Dictionary<ulong, ...>`
- Update method parameters from `uint rawId` to `ulong rawId`

### 4. Removed Obsolete Fields

**Impact**: MEDIUM (if you used these fields)
**Reason**: These fields don't exist in DBN v2 specification

**Removed:**
- `TradingReferencePrice` (long) - Not in DBN spec
- `TradingReferenceDate` (ushort) - Not in DBN spec

**Before (v3.x):**
```csharp
long refPrice = def.TradingReferencePrice;  // ✅ Compiled
ushort refDate = def.TradingReferenceDate;   // ✅ Compiled
```

**After (v4.0.0):**
```csharp
long refPrice = def.TradingReferencePrice;  // ❌ Compilation error
ushort refDate = def.TradingReferenceDate;   // ❌ Compilation error
```

**Migration Action:**
- **Remove** all references to `TradingReferencePrice`
- **Remove** all references to `TradingReferenceDate`
- If needed, use alternative fields like `Activation` or `Expiration` timestamps

### 5. Cached Data Invalidation

**Impact**: HIGH (if you cache instrument data)
**Reason**: All field values are different

**Before (v3.x):**
```csharp
// Cached in database/file from v3.x
var cached = LoadFromCache(instrumentId);
// cached.InstrumentClass = 0 (wrong)
// cached.RawSymbol = "ESH5" (may be truncated)
```

**After (v4.0.0):**
```csharp
// Fresh data from v4.0.0
var fresh = await GetInstrumentDef(instrumentId);
// fresh.InstrumentClass = Future (correct!)
// fresh.RawSymbol = "ESH5" (correct full symbol)

// ⚠️ Values don't match! Must invalidate cache.
```

**Migration Action:**
1. **Delete all cached instrument definition data** before upgrading
2. Re-fetch fresh data after upgrading to v4.0.0
3. Update cache validation logic to check version number
4. Add cache invalidation on version upgrades

**Example cache invalidation:**
```csharp
const string CACHE_VERSION = "4.0.0";

public class CachedInstrumentDef
{
    public string CacheVersion { get; set; } = CACHE_VERSION;
    public InstrumentDefMessage Data { get; set; }
}

public InstrumentDefMessage GetOrFetch(uint instrumentId)
{
    var cached = LoadFromCache(instrumentId);

    // Invalidate if version mismatch
    if (cached?.CacheVersion != CACHE_VERSION)
    {
        cached = FetchFreshData(instrumentId);
        cached.CacheVersion = CACHE_VERSION;
        SaveToCache(cached);
    }

    return cached.Data;
}
```

## New Features (Non-Breaking)

### 13 New Multi-Leg Strategy Fields

If you work with spreads, combos, or other multi-leg instruments, you can now access:

```csharp
// Multi-leg pricing and delta
long legPrice = def.LegPrice;
long legDelta = def.LegDelta;

// Multi-leg ratios
int priceNum = def.LegRatioPriceNumerator;
int priceDenom = def.LegRatioPriceDenominator;
int qtyNum = def.LegRatioQtyNumerator;
int qtyDenom = def.LegRatioQtyDenominator;

// Multi-leg identification
uint legInstrumentId = def.LegInstrumentId;
uint legUnderlyingId = def.LegUnderlyingId;
ushort legCount = def.LegCount;
ushort legIndex = def.LegIndex;

// Multi-leg details
string legSymbol = def.LegRawSymbol;
InstrumentClass legClass = def.LegInstrumentClass;
Side legSide = def.LegSide;

// Strike currency
string strikeCurrency = def.StrikePriceCurrency;
```

**Example - Detecting spreads:**
```csharp
if (def.LegCount > 1)
{
    Console.WriteLine($"Spread with {def.LegCount} legs");
    Console.WriteLine($"Leg #{def.LegIndex}: {def.LegRawSymbol}");
    Console.WriteLine($"Leg Class: {def.LegInstrumentClass}");
    Console.WriteLine($"Leg Side: {def.LegSide}");
}
```

## Step-by-Step Migration Process

### Step 1: Review Your Code

Search your codebase for:

```bash
# Find InstrumentDefMessage usage
grep -r "InstrumentDefMessage" .

# Find InstrumentClass usage
grep -r "InstrumentClass" .

# Find RawInstrumentId usage
grep -r "RawInstrumentId" .

# Find removed fields
grep -r "TradingReferencePrice" .
grep -r "TradingReferenceDate" .
```

### Step 2: Update Type Declarations

**Change 1: RawInstrumentId type**
```diff
- uint rawId = def.RawInstrumentId;
+ ulong rawId = def.RawInstrumentId;

- Dictionary<uint, InstrumentDefMessage> cache;
+ Dictionary<ulong, InstrumentDefMessage> cache;
```

**Change 2: Remove obsolete fields**
```diff
  var price = def.StrikePrice;
- var refPrice = def.TradingReferencePrice;
- var refDate = def.TradingReferenceDate;
```

### Step 3: Update InstrumentClass Logic

**Old code (v3.x):**
```csharp
// DON'T DO THIS - matched everything in v3.x
if (record is InstrumentDefMessage def && def.InstrumentClass == 0)
{
    // Process all instruments
}
```

**New code (v4.0.0):**
```csharp
// Filter for specific instrument types
if (record is InstrumentDefMessage def)
{
    switch (def.InstrumentClass)
    {
        case InstrumentClass.Future:
            ProcessFuture(def);
            break;
        case InstrumentClass.Call:
        case InstrumentClass.Put:
            ProcessOption(def);
            break;
        case InstrumentClass.Stock:
            ProcessStock(def);
            break;
        case InstrumentClass.Unknown:
            // Handle truly undefined instruments
            LogWarning($"Unknown instrument class: {def.RawSymbol}");
            break;
    }
}
```

### Step 4: Invalidate Cached Data

**If you cache instrument definitions:**

```csharp
// BEFORE upgrading to v4.0.0, run this ONCE:
public void InvalidateCacheBeforeUpgrade()
{
    // Delete all cached instrument definitions
    cache.Clear();
    File.Delete("instrument_cache.db");
    Console.WriteLine("Cache invalidated - will be rebuilt after upgrade");
}
```

**After upgrading:**

```csharp
// Let the cache rebuild naturally
var def = await client.GetRangeAsync(..., schema: Schema.Definition).FirstAsync();
// This will now have correct values
```

### Step 5: Update NuGet Package

```bash
dotnet remove package Databento.Client
dotnet add package Databento.Client --version 4.0.0-beta
```

### Step 6: Test Thoroughly

**Test checklist:**

- [ ] Compile without errors
- [ ] InstrumentClass filters work correctly
- [ ] No references to removed fields (`TradingReferencePrice`, `TradingReferenceDate`)
- [ ] `RawInstrumentId` comparisons work with `ulong`
- [ ] Cached data invalidated and rebuilt
- [ ] Integration tests pass with real API data
- [ ] Visual inspection of instrument definition values looks correct

**Example integration test:**

```csharp
[Fact]
public async Task InstrumentClass_IsPopulated_AfterUpgrade()
{
    var client = new HistoricalClient(apiKey);

    var records = await client.GetRangeAsync(
        dataset: "GLBX.MDP3",
        schema: Schema.Definition,
        symbols: new[] { "ES.FUT" },
        startTime: DateTimeOffset.UtcNow.AddDays(-1),
        endTime: DateTimeOffset.UtcNow
    ).Take(10).ToListAsync();

    var defs = records.OfType<InstrumentDefMessage>().ToList();

    // This should PASS in v4.0.0 (would FAIL in v3.x)
    Assert.All(defs, def =>
    {
        Assert.NotEqual(InstrumentClass.Unknown, def.InstrumentClass);
        Assert.Equal(InstrumentClass.Future, def.InstrumentClass);
    });
}
```

## Common Migration Patterns

### Pattern 1: Filtering by Instrument Type

**Before (v3.x):**
```csharp
// Didn't work - all instruments had InstrumentClass = 0
var futures = records
    .OfType<InstrumentDefMessage>()
    .Where(d => d.InstrumentClass == InstrumentClass.Future)
    .ToList();
// Result: Empty list (because all were 0)
```

**After (v4.0.0):**
```csharp
// Now works correctly!
var futures = records
    .OfType<InstrumentDefMessage>()
    .Where(d => d.InstrumentClass == InstrumentClass.Future)
    .ToList();
// Result: Actual futures instruments
```

### Pattern 2: Building Instrument Cache

**Before (v3.x):**
```csharp
Dictionary<uint, InstrumentDefMessage> cache = new();

foreach (var def in instrumentDefs)
{
    cache[def.RawInstrumentId] = def;  // Type: uint
}
```

**After (v4.0.0):**
```csharp
Dictionary<ulong, InstrumentDefMessage> cache = new();  // Changed to ulong

foreach (var def in instrumentDefs)
{
    cache[def.RawInstrumentId] = def;  // Type: ulong
}
```

### Pattern 3: Displaying Instrument Info

**Before (v3.x):**
```csharp
Console.WriteLine($"{def.RawSymbol} - {def.InstrumentClass}");
// Output: "ES" - Unknown  (WRONG - symbol truncated, class always Unknown)
```

**After (v4.0.0):**
```csharp
Console.WriteLine($"{def.RawSymbol} - {def.InstrumentClass}");
// Output: "ESH5" - Future  (CORRECT - full symbol, correct class)
```

## Rollback Plan

If you encounter issues after upgrading:

1. **Downgrade NuGet package:**
   ```bash
   dotnet remove package Databento.Client
   dotnet add package Databento.Client --version 3.0.26-beta
   ```

2. **Restore cached data from backup** (if you backed it up before upgrade)

3. **Revert code changes**

4. **Report issue on GitHub:** https://github.com/Alparse/databento-dotnet/issues

## FAQ

**Q: Do I need to update if I only use trade data (MBO, MBP, OHLCV)?**
A: No. Only `InstrumentDefMessage` changed. Trade schemas are unaffected.

**Q: Will my existing instrument cache still work?**
A: No. You must invalidate and rebuild your cache because field values changed.

**Q: Can I gradually migrate or must I upgrade all at once?**
A: You must upgrade all at once. v3.x and v4.0 return different values for the same data.

**Q: How do I know if my InstrumentClass logic was broken before?**
A: If you filtered by `InstrumentClass == InstrumentClass.Future` in v3.x and got empty results, it was broken. v4.0.0 fixes this.

**Q: Are the new multi-leg fields mandatory?**
A: No. They're additive. If you don't use spreads/combos, you can ignore them.

**Q: What happens to `TradingReferencePrice` and `TradingReferenceDate`?**
A: They're removed because they don't exist in the DBN v2 specification. Your code won't compile if you reference them.

## Need Help?

- **Documentation**: https://docs.databento.com
- **GitHub Issues**: https://github.com/Alparse/databento-dotnet/issues
- **Issue #4**: https://github.com/Alparse/databento-dotnet/issues/4

## Summary

**Required Actions:**
1. ✅ Change `RawInstrumentId` from `uint` to `ulong`
2. ✅ Remove references to `TradingReferencePrice` and `TradingReferenceDate`
3. ✅ Invalidate cached instrument definition data
4. ✅ Update `InstrumentClass` filtering logic
5. ✅ Test thoroughly with real data

**Estimated Migration Time:**
- Small codebase (1-2 usages): 15-30 minutes
- Medium codebase (5-10 usages): 1-2 hours
- Large codebase (extensive usage): 2-4 hours

**Risk Level:** Medium
The changes are straightforward but require careful testing, especially if you cache data or rely heavily on `InstrumentClass` filtering.
