# Response to GitHub Issue #1

Thank you for reporting this issue! We've investigated and confirmed the bug.

## What We Found

The crash occurs in the underlying **databento-cpp native library** when the API returns an error response (invalid symbol, wrong dataset, etc.). Specifically affects:
- `HistoricalClient.GetRangeAsync()`
- `HistoricalClient.GetRangeToFileAsync()`

The bug **cannot be fixed at the .NET wrapper level** - it requires a fix in databento-cpp itself. We've prepared a detailed bug report for the databento-cpp maintainers.

## Testing Results

We tested other APIs with invalid symbols and did not observe similar crashes:
- ✅ **Live API** - handles invalid symbols gracefully via `metadata.not_found`
- ✅ **Batch API** (`BatchSubmitJobAsync`) - throws proper exceptions without crashing

The bug appears specific to the Historical `GetRange` methods.

## Workarounds

### Option 1: Pre-validate symbols (Recommended)
Use the symbology API to validate symbols before querying:

```csharp
var resolution = await client.SymbologyResolveAsync(
    dataset: "GLBX.MDP3",
    symbols: new[] { "CL" },
    stypeIn: SType.RawSymbol,
    stypeOut: SType.InstrumentId,
    startDate: queryDate,
    endDate: queryDate);

if (resolution.NotFound.Count > 0)
{
    // Handle invalid symbols before calling GetRangeAsync
    Console.WriteLine($"Invalid: {string.Join(", ", resolution.NotFound)}");
    return;
}

// Safe to call GetRangeAsync with validated symbols
```

### Option 2: Use Batch API for historical data
For bulk historical downloads, `BatchSubmitJobAsync()` does not have this bug:

```csharp
var job = await client.BatchSubmitJobAsync(
    dataset: "GLBX.MDP3",
    symbols: new[] { "CLZ5" },
    schema: Schema.Trades,
    startTime: start,
    endTime: end);
```

## Status

- We've added warnings to the affected methods' documentation
- Bug report prepared for databento-cpp team
- Will update this issue when databento-cpp releases a fix
