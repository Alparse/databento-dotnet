# Symbology Filtering Example

Filter market data by instrument type using parent symbology.

## Quick Example

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");

await using var client = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();

var startTime = new DateTimeOffset(2024, 11, 1, 13, 30, 0, TimeSpan.FromHours(-5));
var endTime = startTime.AddMinutes(5);

// Query all ES futures using parent symbology
await foreach (var record in client.GetRangeAsync(
    dataset: "GLBX.MDP3",
    schema: Schema.Trades,
    symbols: new[] { "ES.FUT" },      // All E-mini S&P 500 futures
    startTime: startTime,
    endTime: endTime,
    stypeIn: SType.Parent,             // Input: parent symbol
    stypeOut: SType.InstrumentId,      // Output: instrument IDs
    limit: 100))
{
    if (record is TradeMessage trade)
    {
        Console.WriteLine($"Trade: {trade.InstrumentId} @ ${trade.PriceDecimal:F2}");
    }
}
```

## Parent Symbology Format

- **Futures**: `ES.FUT`, `NQ.FUT`, `GC.FUT`
- **Options**: `SPY.OPT`, `QQQ.OPT`, `AAPL.OPT`
- **Spot**: `BTCUSD.SPOT`

## Supported Combinations

### GLBX.MDP3 (CME Futures)
- ✓ `parent` → `instrument_id`
- ✓ `continuous` → `instrument_id`
- ✓ `raw_symbol` → `instrument_id`
- ✓ `instrument_id` → `raw_symbol`

### OPRA.PILLAR (US Options)
- ✓ `parent` → `instrument_id`
- ✓ `raw_symbol` → `instrument_id`
- ✓ `instrument_id` → `raw_symbol`

## Error Handling

```csharp
try
{
    await foreach (var record in client.GetRangeAsync(
        dataset: "GLBX.MDP3",
        schema: Schema.Trades,
        symbols: new[] { "ES.FUT" },
        startTime: start,
        endTime: end,
        stypeIn: SType.Parent,
        stypeOut: SType.InstrumentId))
    {
        // Process records
    }
}
catch (ConnectionException ex)
{
    Console.WriteLine($"API Error: {ex.Message}");
}
```

## See Also

- Full example: `examples/FuturesFilter.Example/`
- Documentation: https://databento.com/docs/standards-and-conventions/symbology
