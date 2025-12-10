# AI Code Agent Guide - Databento.Client

> **For AI coding agents**: This document is optimized for programmatic consumption by agentic code tools (Claude Code CLI, Cursor, GitHub Copilot Workspace, etc.). Use this as your primary reference when working with the Databento.Client library.

**Library**: `Databento.Client` v4.3.0
**Package**: `dotnet add package Databento.Client`
**Runtime**: .NET 8.0 / 9.0
**Platforms**: Windows x64 (NuGet) | Linux/macOS (build from source)

---

## Quick Decision Tree

```
What do you need to do?
│
├─► Stream real-time market data
│   ├─► Push-based (async foreach) → Use LiveClient
│   └─► Pull-based (explicit control) → Use LiveBlockingClient
│
├─► Query historical market data
│   └─► Use HistoricalClient
│
├─► Run backtests with identical code to live
│   ├─► From historical API → Use BacktestingClientBuilder.WithTimeRange()
│   └─► From DBN file (offline) → Use BacktestingClientBuilder.WithFileSource()
│
├─► Get reference data (security master, corporate actions)
│   └─► Use ReferenceClient
│
├─► Read/Write DBN files
│   └─► Use DbnFileReader / DbnFileWriter
│
└─► Resolve symbol names to instrument IDs
    └─► Use HistoricalClient.SymbologyResolveAsync()
```

---

## Essential Namespaces

```csharp
using Databento.Client.Builders;     // All client builders (including BacktestingClientBuilder)
using Databento.Client.Models;       // Record types, enums, Schema, SType
using Databento.Client.Live;         // ILiveClient, ILiveBlockingClient, IPlaybackControllable
using Databento.Client.Historical;   // IHistoricalClient
using Databento.Client.Reference;    // IReferenceClient
using Databento.Client.Resilience;   // RetryPolicy, ResilienceOptions
using Databento.Client.DataSources;  // PlaybackSpeed, PlaybackController (for backtesting)
using Databento.Client.Dbn;          // DbnFileReader, DbnFileWriter
using Databento.Client.Metadata;     // ITsSymbolMap, IPitSymbolMap
```

---

## Client Construction Templates

### LiveClient (Push-Based Streaming)
```csharp
await using var client = new LiveClientBuilder()
    .WithKeyFromEnv()              // Reads DATABENTO_API_KEY env var
    .WithAutoReconnect()           // Enable resilience (recommended)
    .Build();
```

### LiveBlockingClient (Pull-Based Streaming)
```csharp
await using var client = new LiveBlockingClientBuilder()
    .WithKeyFromEnv()
    .WithDataset("EQUS.MINI")      // REQUIRED for LiveBlockingClient
    .Build();
```

### HistoricalClient
```csharp
await using var client = new HistoricalClientBuilder()
    .WithKeyFromEnv()
    .Build();
```

### ReferenceClient
```csharp
var client = new ReferenceClientBuilder()
    .WithKeyFromEnv()
    .Build();
```

### BacktestingClient (Historical API)
```csharp
var start = new DateTimeOffset(2025, 1, 15, 9, 30, 0, TimeSpan.FromHours(-5));
var end = start.AddHours(6.5);

await using var client = new BacktestingClientBuilder()
    .WithKeyFromEnv()
    .WithTimeRange(start, end)
    .WithDiskCache()              // Cache for repeated runs (optional)
    .Build();
```

### BacktestingClient (File-Based, Offline)
```csharp
await using var client = new BacktestingClientBuilder()
    .WithFileSource("/path/to/data.dbn")  // No API key needed
    .Build();
```

---

## Common Operations

### 1. Live Streaming (Most Common)

```csharp
await using var client = new LiveClientBuilder()
    .WithKeyFromEnv()
    .WithAutoReconnect()
    .Build();

await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA", "AAPL" });
await client.StartAsync();

await foreach (var record in client.StreamAsync())
{
    if (record is TradeMessage trade)
        Console.WriteLine($"{trade.InstrumentId}: ${trade.PriceDecimal} x {trade.Size}");
}
```

### 2. Historical Data Query

```csharp
await using var client = new HistoricalClientBuilder()
    .WithKeyFromEnv()
    .Build();

var start = new DateTimeOffset(2025, 1, 15, 14, 30, 0, TimeSpan.Zero);
var end = start.AddHours(1);

await foreach (var record in client.GetRangeAsync(
    "EQUS.MINI", Schema.Trades, new[] { "NVDA" }, start, end))
{
    if (record is TradeMessage trade)
        Console.WriteLine($"${trade.PriceDecimal}");
}
```

### 3. Backtesting (Same Code as Live)

```csharp
// Strategy code works identically with live or backtest clients
async Task RunStrategy(ILiveClient client)
{
    await foreach (var record in client.StreamAsync())
    {
        if (record is TradeMessage trade)
            ProcessTrade(trade);
    }
}

// Backtest mode
var start = new DateTimeOffset(2025, 1, 15, 9, 30, 0, TimeSpan.FromHours(-5));
var end = start.AddHours(6.5);

await using var client = new BacktestingClientBuilder()
    .WithKeyFromEnv()
    .WithTimeRange(start, end)
    .WithDiskCache()  // Cache for repeated runs
    .Build();

await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA" });
await client.StartAsync();
await RunStrategy(client);  // Same code as live!
```

### 4. Symbol Mapping (CRITICAL PATTERN)

**Live clients receive `SymbolMappingMessage` records. Historical API does NOT.**

#### For Live Clients:
```csharp
var symbolMap = new ConcurrentDictionary<uint, string>();

client.DataReceived += (sender, e) =>
{
    if (e.Record is SymbolMappingMessage mapping)
    {
        // IMPORTANT: Use STypeOutSymbol (NOT STypeInSymbol)
        symbolMap[mapping.InstrumentId] = mapping.STypeOutSymbol;
        return;
    }

    if (e.Record is TradeMessage trade)
    {
        var symbol = symbolMap.GetValueOrDefault(trade.InstrumentId, "UNKNOWN");
        Console.WriteLine($"{symbol}: ${trade.PriceDecimal}");
    }
};
```

#### For Historical Clients:
```csharp
// Step 1: Resolve symbols BEFORE streaming
var queryDate = DateOnly.FromDateTime(start.Date);
var resolution = await client.SymbologyResolveAsync(
    "EQUS.MINI", symbols, SType.RawSymbol, SType.InstrumentId,
    queryDate, queryDate.AddDays(1));

var symbolMap = new Dictionary<uint, string>();
foreach (var (inputSymbol, intervals) in resolution.Mappings)
    foreach (var interval in intervals)
        if (uint.TryParse(interval.Symbol, out var instrumentId))
            symbolMap[instrumentId] = inputSymbol;

// Step 2: Stream data using pre-built map
await foreach (var record in client.GetRangeAsync(...))
{
    if (record is TradeMessage trade)
    {
        var symbol = symbolMap.GetValueOrDefault(trade.InstrumentId, "UNKNOWN");
        // Use symbol...
    }
}
```

---

## Schema Reference (20 Schemas)

| Schema | Enum | Record Type | Use Case |
|--------|------|-------------|----------|
| `mbo` | `Schema.Mbo` | `MboMessage` | Full order book |
| `mbp-1` | `Schema.Mbp1` | `Mbp1Message` | Top of book |
| `mbp-10` | `Schema.Mbp10` | `Mbp10Message` | 10 price levels |
| `trades` | `Schema.Trades` | `TradeMessage` | All trades |
| `tbbo` | `Schema.Tbbo` | `TbboMessage` | Trades with BBO |
| `ohlcv-1s` | `Schema.Ohlcv1S` | `OhlcvMessage` | 1-second bars |
| `ohlcv-1m` | `Schema.Ohlcv1M` | `OhlcvMessage` | 1-minute bars |
| `ohlcv-1h` | `Schema.Ohlcv1H` | `OhlcvMessage` | 1-hour bars |
| `ohlcv-1d` | `Schema.Ohlcv1D` | `OhlcvMessage` | Daily bars |
| `ohlcv-eod` | `Schema.OhlcvEod` | `OhlcvMessage` | End-of-day bars |
| `definition` | `Schema.Definition` | `InstrumentDefMessage` | Instrument info |
| `statistics` | `Schema.Statistics` | `StatMessage` | Market stats |
| `status` | `Schema.Status` | `StatusMessage` | Trading status |
| `imbalance` | `Schema.Imbalance` | `ImbalanceMessage` | Auction imbalance |
| `cmbp-1` | `Schema.Cmbp1` | `Cmbp1Message` | Consolidated MBP-1 |
| `cbbo-1s` | `Schema.Cbbo1S` | `CbboMessage` | Consolidated BBO 1s |
| `cbbo-1m` | `Schema.Cbbo1M` | `CbboMessage` | Consolidated BBO 1m |
| `tcbbo` | `Schema.Tcbbo` | `TcbboMessage` | Trades with CBBO |
| `bbo-1s` | `Schema.Bbo1S` | `BboMessage` | BBO 1-second |
| `bbo-1m` | `Schema.Bbo1M` | `BboMessage` | BBO 1-minute |

---

## Record Type Pattern Matching

```csharp
await foreach (var record in client.StreamAsync())
{
    switch (record)
    {
        case TradeMessage trade:
            // Price as decimal: trade.PriceDecimal
            // Raw price (fixed-point): trade.Price (divide by 1_000_000_000)
            break;

        case Mbp1Message mbp:
            // Best bid: mbp.Level.BidPrice, mbp.Level.BidSize
            // Best ask: mbp.Level.AskPrice, mbp.Level.AskSize
            break;

        case Mbp10Message mbp10:
            // 10 levels: mbp10.Levels[0..9]
            break;

        case OhlcvMessage ohlcv:
            // ohlcv.Open, .High, .Low, .Close, .Volume
            break;

        case SymbolMappingMessage mapping:
            // mapping.InstrumentId, mapping.STypeOutSymbol
            break;

        case InstrumentDefMessage def:
            // def.RawSymbol, def.InstrumentClass, def.StrikePrice
            break;

        case StatusMessage status:
            // status.Action (PreOpen, Trading, Halt, Close, etc.)
            break;

        case ErrorMessage error:
            // error.Error, error.Code
            break;

        case SystemMessage system:
            // system.Code (Heartbeat, SubscriptionAck, etc.)
            break;
    }
}
```

---

## Price Conversion

All prices are stored as `long` with 9 decimal places (fixed-point).

```csharp
// Use convenience properties (recommended):
decimal price = trade.PriceDecimal;

// Or manual conversion:
decimal price = trade.Price / 1_000_000_000m;

// For BidAskPair:
decimal bidPrice = mbp.Level.BidPrice / 1_000_000_000m;
```

---

## Resilience Configuration

```csharp
// Simple auto-reconnect
await using var client = new LiveClientBuilder()
    .WithKeyFromEnv()
    .WithAutoReconnect()
    .Build();

// Full resilience configuration
var options = new ResilienceOptions
{
    AutoReconnect = true,
    AutoResubscribe = true,
    RetryPolicy = RetryPolicy.Aggressive,  // 5 retries, up to 60s delay
    HeartbeatTimeout = TimeSpan.FromSeconds(60),
    OnReconnecting = (attempt, ex) => { Console.WriteLine($"Retry {attempt}"); return true; },
    OnReconnected = (attempts) => Console.WriteLine($"Reconnected after {attempts}"),
    OnReconnectFailed = (ex) => Console.WriteLine($"Failed: {ex.Message}")
};

await using var client = new LiveClientBuilder()
    .WithKeyFromEnv()
    .WithResilienceOptions(options)
    .Build();
```

**RetryPolicy Options:**
- `RetryPolicy.Default`: 3 retries, 1s initial, 30s max
- `RetryPolicy.Aggressive`: 5 retries, 1s initial, 60s max
- `RetryPolicy.None`: No retries

---

## Common Datasets

| Dataset | Description |
|---------|-------------|
| `EQUS.MINI` | US equities (mini, lower cost) |
| `EQUS.BASIC` | US equities (basic) |
| `EQUS.MAX` | US equities (full depth) |
| `GLBX.MDP3` | CME Group futures |
| `OPRA.PILLAR` | US options (OPRA) |
| `XNAS.ITCH` | NASDAQ TotalView |
| `XNYS.PILLAR` | NYSE Integrated |
| `DBEQ.BASIC` | Databento equities (consolidated) |

---

## API Key Configuration

**Environment Variable (Recommended):**
```bash
# Windows PowerShell
$env:DATABENTO_API_KEY="your-api-key"

# Linux/macOS
export DATABENTO_API_KEY="your-api-key"
```

**Code:**
```csharp
// From environment (recommended)
.WithKeyFromEnv()

// Direct (NOT for production)
.WithApiKey("your-api-key")
```

---

## Common Pitfalls & Anti-Patterns

### 1. Wrong Symbol Property in SymbolMappingMessage
```csharp
// WRONG - STypeInSymbol contains your subscription string (e.g., "ALL_SYMBOLS")
symbolMap[mapping.InstrumentId] = mapping.STypeInSymbol;

// CORRECT - STypeOutSymbol contains the actual ticker
symbolMap[mapping.InstrumentId] = mapping.STypeOutSymbol;
```

### 2. Expecting SymbolMappingMessage from Historical API
```csharp
// WRONG - Historical API does NOT send SymbolMappingMessage
await foreach (var record in client.GetRangeAsync(...))
{
    if (record is SymbolMappingMessage) // Never true for historical
        // ...
}

// CORRECT - Use SymbologyResolveAsync() before streaming
var resolution = await client.SymbologyResolveAsync(...);
// Build symbol map from resolution.Mappings
```

### 3. Forgetting Dataset for LiveBlockingClient
```csharp
// WRONG - Will throw exception
var client = new LiveBlockingClientBuilder()
    .WithKeyFromEnv()
    .Build();

// CORRECT - Dataset is REQUIRED
var client = new LiveBlockingClientBuilder()
    .WithKeyFromEnv()
    .WithDataset("EQUS.MINI")  // Required
    .Build();
```

### 4. Not Disposing Clients
```csharp
// WRONG - Resource leak
var client = new LiveClientBuilder().WithKeyFromEnv().Build();
// client never disposed

// CORRECT - Use await using
await using var client = new LiveClientBuilder()
    .WithKeyFromEnv()
    .Build();
```

### 5. Starting Without Subscribing
```csharp
// WRONG - No subscriptions, no data
await client.StartAsync();
await foreach (var record in client.StreamAsync()) { }

// CORRECT - Subscribe before starting
await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA" });
await client.StartAsync();
await foreach (var record in client.StreamAsync()) { }
```

---

## Method Signatures Quick Reference

### LiveClient
```csharp
Task SubscribeAsync(string dataset, Schema schema, IEnumerable<string> symbols,
                    DateTimeOffset? startTime = null, CancellationToken ct = default);
Task<DbnMetadata> StartAsync(CancellationToken ct = default);
IAsyncEnumerable<Record> StreamAsync(CancellationToken ct = default);
Task StopAsync(CancellationToken ct = default);
Task BlockUntilStoppedAsync(CancellationToken ct = default);
Task<bool> BlockUntilStoppedAsync(TimeSpan timeout, CancellationToken ct = default);
Task ReconnectAsync(CancellationToken ct = default);
Task ResubscribeAsync(CancellationToken ct = default);
```

### LiveBlockingClient
```csharp
Task SubscribeAsync(string dataset, Schema schema, IEnumerable<string> symbols,
                    CancellationToken ct = default);
Task<DbnMetadata> StartAsync(CancellationToken ct = default);
Task<Record?> NextRecordAsync(TimeSpan? timeout = null, CancellationToken ct = default);
Task StopAsync(CancellationToken ct = default);
```

### HistoricalClient
```csharp
IAsyncEnumerable<Record> GetRangeAsync(string dataset, Schema schema,
    IEnumerable<string> symbols, DateTimeOffset startTime, DateTimeOffset endTime,
    CancellationToken ct = default);

Task<string> GetRangeToFileAsync(string filePath, string dataset, Schema schema,
    IEnumerable<string> symbols, DateTimeOffset startTime, DateTimeOffset endTime,
    CancellationToken ct = default);

Task<SymbologyResolution> SymbologyResolveAsync(string dataset,
    IEnumerable<string> symbols, SType stypeIn, SType stypeOut,
    DateOnly startDate, DateOnly endDate, CancellationToken ct = default);

Task<IReadOnlyList<string>> ListDatasetsAsync(string? venue = null,
    CancellationToken ct = default);

Task<decimal> GetCostAsync(string dataset, Schema schema,
    DateTimeOffset startTime, DateTimeOffset endTime,
    IEnumerable<string> symbols, CancellationToken ct = default);
```

### ReferenceClient
```csharp
// Security Master
Task<List<SecurityMasterRecord>> SecurityMaster.GetLastAsync(
    IEnumerable<string>? symbols = null, SType stypeIn = SType.RawSymbol,
    CancellationToken ct = default);

// Corporate Actions
Task<List<CorporateActionRecord>> CorporateActions.GetRangeAsync(
    DateTimeOffset start, DateTimeOffset? end = null,
    IEnumerable<string>? symbols = null, CancellationToken ct = default);

// Adjustment Factors
Task<List<AdjustmentFactorRecord>> AdjustmentFactors.GetRangeAsync(
    DateTimeOffset start, DateTimeOffset? end = null,
    IEnumerable<string>? symbols = null, CancellationToken ct = default);
```

---

## Enums Quick Reference

### Schema (Data Type)
`Mbo`, `Mbp1`, `Mbp10`, `Tbbo`, `Trades`, `Ohlcv1S`, `Ohlcv1M`, `Ohlcv1H`, `Ohlcv1D`, `OhlcvEod`, `Definition`, `Statistics`, `Status`, `Imbalance`, `Cmbp1`, `Cbbo1S`, `Cbbo1M`, `Tcbbo`, `Bbo1S`, `Bbo1M`

### SType (Symbology Type)
`InstrumentId`, `RawSymbol`, `Smart`, `Continuous`, `Parent`, `NasdaqSymbol`, `CmsSymbol`, `Isin`, `UsCode`, `BbgCompId`, `BbgCompTicker`, `Figi`, `FigiTicker`

### Side
`Ask` ('A'), `Bid` ('B'), `None` ('N')

### Action
`Add` ('A'), `Modify` ('M'), `Cancel` ('C'), `Trade` ('T'), `Fill` ('F'), `Clear` ('R'), `None` ('N')

### InstrumentClass
`Stock` ('K'), `Future` ('F'), `Call` ('C'), `Put` ('P'), `Bond` ('B'), `FutureSpread` ('S'), `OptionSpread` ('T'), `MixedSpread` ('M'), `FxSpot` ('X'), `Unknown` (0)

---

## DBN File Operations

### Reading DBN Files
```csharp
await using var reader = DbnFileReader.Open("data.dbn");
var metadata = reader.GetMetadata();

await foreach (var record in reader.ReadRecordsAsync())
{
    Console.WriteLine(record);
}
```

### Writing DBN Files
```csharp
await using var writer = DbnFileWriter.Create("output.dbn", metadata);
writer.WriteRecord(record);
writer.WriteRecords(records);
```

---

## Events (LiveClient Only)

```csharp
client.DataReceived += (sender, e) =>
{
    Record record = e.Record;
    // Process record...
};

client.ErrorOccurred += (sender, e) =>
{
    Exception exception = e.Exception;
    // Handle error...
};
```

---

## BlockUntilStoppedAsync (Event-Based Streaming)

Use `BlockUntilStoppedAsync` when streaming with events (`DataReceived`) instead of `StreamAsync`.
It blocks until `StopAsync()` is called from elsewhere (e.g., event handler, background task, Ctrl+C).

### Usage Pattern

```csharp
await using var client = new LiveClientBuilder()
    .WithKeyFromEnv()
    .Build();

// Process records via event handler
client.DataReceived += (s, e) =>
{
    if (e.Record is TradeMessage trade)
    {
        ProcessTrade(trade);
        if (shouldStop)
            client.StopAsync();  // This unblocks BlockUntilStoppedAsync
    }
};

await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA" });
await client.StartAsync();

// Block until StopAsync is called
await client.BlockUntilStoppedAsync();
```

### With Timeout

```csharp
// Stream for up to 5 minutes
bool stopped = await client.BlockUntilStoppedAsync(TimeSpan.FromSeconds(300));
if (!stopped)
{
    // Timeout reached - stop manually
    await client.StopAsync();
}
```

### When NOT Needed

If using `await foreach (var record in client.StreamAsync())`, you don't need `BlockUntilStoppedAsync` - the enumeration already blocks until the stream ends.

---

## Client Lifecycle

**Important:** Live clients cannot be restarted after `StopAsync()`. Create a new client instance for each session.

```csharp
// Session 1
await using var client1 = new LiveClientBuilder().WithKeyFromEnv().Build();
await client1.SubscribeAsync(...);
await client1.StartAsync();
// ... stream data ...
await client1.StopAsync();
// client1 is now stopped - cannot restart

// Session 2 - create new client
await using var client2 = new LiveClientBuilder().WithKeyFromEnv().Build();
await client2.SubscribeAsync(...);
await client2.StartAsync();
// ... stream data ...
```

---

## Intraday Replay Mode

Stream historical data from market open, then continue with live data:

```csharp
// Pass DateTimeOffset.MinValue as startTime for intraday replay
await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA" },
    startTime: DateTimeOffset.MinValue);  // Replays from most recent market open
```

**Note**: Only available within 24 hours of last market open.

---

## Testing Connection

```csharp
// Quick connection test
var client = new HistoricalClientBuilder().WithKeyFromEnv().Build();
var datasets = await client.ListDatasetsAsync();
Console.WriteLine($"Connected! Found {datasets.Count} datasets");
```

---

## Project Structure

```
databento-dotnet/
├── src/
│   ├── Databento.Client/        # High-level .NET API (use this)
│   │   ├── Builders/            # Client builders
│   │   ├── Historical/          # HistoricalClient
│   │   ├── Live/                # LiveClient, LiveBlockingClient
│   │   ├── Reference/           # ReferenceClient
│   │   ├── Models/              # Record types, enums
│   │   ├── Resilience/          # Retry policies
│   │   ├── Dbn/                 # DBN file I/O
│   │   └── Metadata/            # Symbol maps
│   ├── Databento.Interop/       # P/Invoke layer (internal)
│   └── Databento.Native/        # C++ wrapper (internal)
└── examples/                    # Working examples
```

---

## Troubleshooting

### DllNotFoundException
```bash
# Ensure VC++ Runtime is installed (Windows)
# Download: https://aka.ms/vs/17/release/vc_redist.x64.exe

# Force restore packages
dotnet restore --force
```

### Connection Timeout
```csharp
var client = new HistoricalClientBuilder()
    .WithKeyFromEnv()
    .WithTimeout(TimeSpan.FromMinutes(5))  // Increase timeout
    .Build();
```

### Enable Logging
```csharp
var client = new LiveClientBuilder()
    .WithKeyFromEnv()
    .WithLogger(loggerFactory.CreateLogger<ILiveClient>())
    .Build();
```

---

## Complete Working Examples

### Example 1: Live Trade Streaming with Symbol Resolution
```csharp
using System.Collections.Concurrent;
using Databento.Client.Builders;
using Databento.Client.Models;

var symbolMap = new ConcurrentDictionary<uint, string>();

await using var client = new LiveClientBuilder()
    .WithKeyFromEnv()
    .WithAutoReconnect()
    .Build();

client.DataReceived += (sender, e) =>
{
    if (e.Record is SymbolMappingMessage mapping)
    {
        symbolMap[mapping.InstrumentId] = mapping.STypeOutSymbol;
        Console.WriteLine($"Mapped {mapping.InstrumentId} to {mapping.STypeOutSymbol}");
    }
};

await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA", "AAPL" });
await client.StartAsync();

await foreach (var record in client.StreamAsync())
{
    if (record is TradeMessage trade)
    {
        var symbol = symbolMap.GetValueOrDefault(trade.InstrumentId, "UNKNOWN");
        Console.WriteLine($"{symbol}: ${trade.PriceDecimal} x {trade.Size}");
    }
}
```

### Example 2: Historical Data with Cost Estimation
```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

await using var client = new HistoricalClientBuilder()
    .WithKeyFromEnv()
    .Build();

var symbols = new[] { "NVDA" };
var start = new DateTimeOffset(2025, 1, 15, 14, 30, 0, TimeSpan.Zero);
var end = start.AddHours(1);

// Check cost before querying
var cost = await client.GetCostAsync("EQUS.MINI", Schema.Trades, start, end, symbols);
Console.WriteLine($"Estimated cost: ${cost:F4}");

// Query data
await foreach (var record in client.GetRangeAsync("EQUS.MINI", Schema.Trades, symbols, start, end))
{
    if (record is TradeMessage trade)
        Console.WriteLine($"${trade.PriceDecimal} x {trade.Size}");
}
```

### Example 3: Reference Data Query
```csharp
using Databento.Client.Builders;

var client = new ReferenceClientBuilder()
    .WithKeyFromEnv()
    .Build();

// Security master
var securities = await client.SecurityMaster.GetLastAsync(symbols: new[] { "NVDA" });
foreach (var sec in securities)
    Console.WriteLine($"{sec.RawSymbol}: {sec.SecurityType}");

// Corporate actions (last year)
var actions = await client.CorporateActions.GetRangeAsync(
    start: DateTimeOffset.UtcNow.AddYears(-1),
    symbols: new[] { "NVDA" });

// Adjustment factors (last 90 days)
var factors = await client.AdjustmentFactors.GetRangeAsync(
    start: DateTimeOffset.UtcNow.AddDays(-90),
    symbols: new[] { "NVDA" });
```

---

## See Also

- [README.md](README.md) - User-facing documentation
- [Backtesting Reference](docs/backtesting_reference.md) - Complete backtesting guide with playback control
- [API_REFERENCE.md](API_REFERENCE.md) - Quick API reference
- [API_Classification.md](API_Classification.md) - Complete API signatures
- [Databento Docs](https://databento.com/docs/) - Official documentation
