# Futures/Options Filter Example - Using Parent Symbology

This example demonstrates how to use the `stypeIn` parameter with parent symbology to filter data by instrument type.

## What is Parent Symbology?

Parent symbology lets you query **all related instruments** with a single symbol:
- `ES.FUT` - All E-mini S&P 500 futures and spreads
- `QQQ.OPT` - All QQQ options (calls, puts, spreads)

Format: `[ASSET].FUT` for futures, `[ASSET].OPT` for options

## How to Find Available Parent Symbols

### Method 1: Query Instrument Definitions

```csharp
// Query ALL_SYMBOLS with definition schema
await foreach (var record in client.GetRangeAsync(
    dataset: "OPRA.PILLAR",
    schema: Schema.Definition,
    symbols: new[] { "ALL_SYMBOLS" },
    startTime: date,
    endTime: date.AddDays(1)))
{
    if (record is InstrumentDefMessage def)
    {
        // Parent symbol = def.Asset + ".OPT" or ".FUT"
        Console.WriteLine($"{def.Asset}.OPT");
    }
}
```

See `GetParentSymbols.cs` for a complete implementation.

### Method 2: Check Databento Documentation

Visit dataset-specific docs:
- OPRA.PILLAR: https://databento.com/docs/venues-and-datasets/opra-pillar
- GLBX.MDP3: https://databento.com/docs/venues-and-datasets/glbx-mdp3

### Method 3: Common Parent Symbols

**OPRA.PILLAR (US Options)**
```
SPY.OPT  - SPDR S&P 500 ETF
QQQ.OPT  - Invesco QQQ ETF (Nasdaq-100)
IWM.OPT  - iShares Russell 2000 ETF
AAPL.OPT - Apple Inc.
TSLA.OPT - Tesla Inc.
NVDA.OPT - NVIDIA Corp.
MSFT.OPT - Microsoft Corp.
AMZN.OPT - Amazon.com Inc.
GOOGL.OPT - Alphabet Inc.
META.OPT - Meta Platforms Inc.
```

**GLBX.MDP3 (CME Futures)**
```
ES.FUT   - E-mini S&P 500
NQ.FUT   - E-mini Nasdaq-100
YM.FUT   - E-mini Dow Jones
RTY.FUT  - E-mini Russell 2000
MES.FUT  - Micro E-mini S&P 500
MNQ.FUT  - Micro E-mini Nasdaq-100
GC.FUT   - Gold
SI.FUT   - Silver
CL.FUT   - Crude Oil
NG.FUT   - Natural Gas
ZB.FUT   - 30-Year US Treasury Bond
ZN.FUT   - 10-Year US Treasury Note
```

## Supported Symbology Combinations

### OPRA.PILLAR
| Input | Output | Supported |
|-------|--------|-----------|
| `parent` | `instrument_id` | ✓ |
| `raw_symbol` | `instrument_id` | ✓ |
| `instrument_id` | `raw_symbol` | ✓ |

### GLBX.MDP3
| Input | Output | Supported |
|-------|--------|-----------|
| `parent` | `instrument_id` | ✓ |
| `continuous` | `instrument_id` | ✓ |
| `raw_symbol` | `instrument_id` | ✓ |
| `instrument_id` | `raw_symbol` | ✓ |

## Examples

### Query All QQQ Options
```csharp
await foreach (var record in client.GetRangeAsync(
    dataset: "OPRA.PILLAR",
    schema: Schema.Trades,
    symbols: new[] { "QQQ.OPT" },
    startTime: start,
    endTime: end,
    stypeIn: SType.Parent,
    stypeOut: SType.InstrumentId))
{
    if (record is TradeMessage trade)
    {
        // trade.InstrumentId identifies the specific option contract
        Console.WriteLine($"InstrumentId: {trade.InstrumentId}");
    }
}
```

### Query All ES Futures
```csharp
await foreach (var record in client.GetRangeAsync(
    dataset: "GLBX.MDP3",
    schema: Schema.Trades,
    symbols: new[] { "ES.FUT" },
    startTime: start,
    endTime: end,
    stypeIn: SType.Parent,
    stypeOut: SType.InstrumentId))
{
    if (record is TradeMessage trade)
    {
        // trade.InstrumentId identifies the specific futures contract
        Console.WriteLine($"InstrumentId: {trade.InstrumentId}");
    }
}
```

## Running the Example

```bash
# Set your API key
export DATABENTO_API_KEY="your-api-key"

# Run the main example
dotnet run

# Or discover all available parent symbols
# (uncomment GetParentSymbols.RunExampleAsync() in Program.cs)
```

## Resources

- [Databento Symbology Documentation](https://databento.com/docs/standards-and-conventions/symbology)
- [Parent Symbology Guide](https://databento.com/docs/examples/symbology/parent-symbology)
- [OPRA.PILLAR Dataset](https://databento.com/docs/venues-and-datasets/opra-pillar)
- [GLBX.MDP3 Dataset](https://databento.com/docs/venues-and-datasets/glbx-mdp3)
