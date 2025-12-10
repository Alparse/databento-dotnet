# databento-dotnet API Reference

**Version:** v4.3.0 | [Detailed API Classification](API_Classification.md)

---

## Quick Start

```csharp
// Live streaming
await using var live = new LiveClientBuilder().WithKeyFromEnv().Build();
await live.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA" });
await live.StartAsync();
await foreach (var record in live.StreamAsync())
    if (record is TradeMessage trade)
        Console.WriteLine($"{trade.InstrumentId}: ${trade.PriceDecimal}");

// Historical data
await using var hist = new HistoricalClientBuilder().WithKeyFromEnv().Build();
await foreach (var record in hist.GetRangeAsync("EQUS.MINI", Schema.Trades,
    new[] { "NVDA" }, start, end))
    Console.WriteLine(record);
```

---

## Clients Overview

| Client | Purpose | Pattern |
|--------|---------|---------|
| `LiveClient` | Real-time streaming | Push-based (`IAsyncEnumerable`) |
| `LiveBlockingClient` | Real-time with explicit control | Pull-based (`NextRecordAsync`) |
| `HistoricalClient` | Time-range queries | Streaming (`IAsyncEnumerable`) |
| `ReferenceClient` | Security master, corporate actions | REST API |

---

## 1. LiveClient

Push-based streaming with `IAsyncEnumerable`.

### Builder

```csharp
await using var client = new LiveClientBuilder()
    .WithKeyFromEnv()                    // or .WithApiKey(key)
    .WithDataset("EQUS.MINI")            // Optional default dataset
    .WithAutoReconnect()                 // Enable resilience
    .WithRetryPolicy(RetryPolicy.Aggressive)
    .Build();
```

| Method | Description |
|--------|-------------|
| `WithApiKey(string)` | Set API key |
| `WithKeyFromEnv()` | Use DATABENTO_API_KEY env var |
| `WithDataset(string)` | Default dataset |
| `WithAutoReconnect(bool)` | Enable auto-reconnect |
| `WithRetryPolicy(RetryPolicy)` | Retry configuration |
| `WithHeartbeatTimeout(TimeSpan)` | Stale connection timeout |
| `WithResilienceOptions(ResilienceOptions)` | Full resilience config |
| `WithLogger(ILogger)` | Enable logging |

### Methods

```csharp
// Subscribe
await client.SubscribeAsync(dataset, schema, symbols);
await client.SubscribeAsync(dataset, schema, symbols, startTime);  // Intraday replay

// Control
await client.StartAsync();           // Returns DbnMetadata
await client.StopAsync();            // Stop streaming (cannot restart - create new client)
await client.ReconnectAsync();
await client.ResubscribeAsync();

// Block until stopped (for event-based streaming)
await client.BlockUntilStoppedAsync();                    // Block indefinitely
bool stopped = await client.BlockUntilStoppedAsync(timeout);  // Block with timeout

// Stream
await foreach (var record in client.StreamAsync()) { }
```

### Events

```csharp
client.DataReceived += (s, e) => Console.WriteLine(e.Record);
client.ErrorOccurred += (s, e) => Console.WriteLine(e.Exception);
```

### BlockUntilStoppedAsync (Event-Based Streaming)

Use when streaming with events instead of `StreamAsync`:

```csharp
client.DataReceived += (s, e) => {
    if (e.Record is TradeMessage trade)
        ProcessTrade(trade);
    if (shouldStop)
        client.StopAsync();  // Unblocks BlockUntilStoppedAsync
};

await client.StartAsync();
await client.BlockUntilStoppedAsync();  // Blocks until StopAsync is called
```

### Client Lifecycle

**Important:** Clients cannot be restarted after `StopAsync()`. Create a new instance for each session.

---

## 2. LiveBlockingClient

Pull-based streaming with explicit record retrieval.

### Builder

```csharp
await using var client = new LiveBlockingClientBuilder()
    .WithKeyFromEnv()
    .WithDataset("EQUS.MINI")    // Required
    .Build();
```

### Methods

```csharp
await client.SubscribeAsync(dataset, schema, symbols);
await client.StartAsync();

while (true)
{
    var record = await client.NextRecordAsync(timeout: TimeSpan.FromSeconds(5));
    if (record == null) break;  // Timeout
    Console.WriteLine(record);
}
```

---

## 3. HistoricalClient

Query historical market data.

### Builder

```csharp
await using var client = new HistoricalClientBuilder()
    .WithKeyFromEnv()
    .WithTimeout(TimeSpan.FromMinutes(5))
    .Build();
```

### Time-Range Queries

```csharp
// Stream records
await foreach (var record in client.GetRangeAsync(
    dataset, schema, symbols, startTime, endTime))
{
    Console.WriteLine(record);
}

// Save to file
await client.GetRangeToFileAsync(filePath, dataset, schema, symbols, start, end);
```

### Metadata & Billing

```csharp
var datasets = await client.ListDatasetsAsync();
var schemas = await client.ListSchemasAsync("EQUS.MINI");
var cost = await client.GetCostAsync(dataset, schema, start, end, symbols);
var count = await client.GetRecordCountAsync(dataset, schema, start, end, symbols);
```

### Symbology Resolution

```csharp
var resolution = await client.SymbologyResolveAsync(
    dataset, symbols, SType.RawSymbol, SType.InstrumentId, startDate, endDate);

foreach (var (symbol, intervals) in resolution.Mappings)
    foreach (var interval in intervals)
        Console.WriteLine($"{symbol} -> {interval.Symbol}");
```

---

## 4. ReferenceClient

Query reference data (security master, corporate actions, adjustments).

### Builder

```csharp
var client = new ReferenceClientBuilder()
    .WithKeyFromEnv()
    .Build();
```

### APIs

```csharp
// Security Master
var securities = await client.SecurityMaster.GetLastAsync(symbols: new[] { "NVDA" });

// Corporate Actions
var actions = await client.CorporateActions.GetRangeAsync(
    start: DateTimeOffset.UtcNow.AddYears(-1), symbols: new[] { "NVDA" });

// Adjustment Factors
var factors = await client.AdjustmentFactors.GetRangeAsync(
    start: DateTimeOffset.UtcNow.AddDays(-90), symbols: new[] { "NVDA" });
```

---

## 5. Resilience

### RetryPolicy

| Policy | Retries | Initial Delay | Max Delay |
|--------|---------|---------------|-----------|
| `RetryPolicy.Default` | 3 | 1s | 30s |
| `RetryPolicy.Aggressive` | 5 | 1s | 60s |
| `RetryPolicy.None` | 0 | - | - |

### ResilienceOptions

```csharp
var options = new ResilienceOptions
{
    AutoReconnect = true,
    AutoResubscribe = true,
    RetryPolicy = RetryPolicy.Aggressive,
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

---

## 6. Record Types

All records inherit from `Record` with common properties:

```csharp
public abstract class Record
{
    public long TimestampNs { get; }
    public uint InstrumentId { get; }
    public DateTimeOffset Timestamp { get; }  // Convenience
}
```

### Common Records

| Type | Schema | Key Properties |
|------|--------|----------------|
| `TradeMessage` | `trades` | `Price`, `Size`, `Side`, `PriceDecimal` |
| `Mbp1Message` | `mbp-1` | `Level` (BidAskPair) |
| `Mbp10Message` | `mbp-10` | `Levels[10]` |
| `OhlcvMessage` | `ohlcv-*` | `Open`, `High`, `Low`, `Close`, `Volume` |
| `InstrumentDefMessage` | `definition` | `RawSymbol`, `InstrumentClass`, `StrikePrice` |
| `SymbolMappingMessage` | - | `STypeOutSymbol`, `InstrumentId` |

### Pattern Matching

```csharp
await foreach (var record in client.StreamAsync())
{
    switch (record)
    {
        case TradeMessage trade:
            Console.WriteLine($"Trade: ${trade.PriceDecimal} x {trade.Size}");
            break;
        case Mbp1Message mbp:
            Console.WriteLine($"Bid: {mbp.Level.BidPriceDecimal} Ask: {mbp.Level.AskPriceDecimal}");
            break;
        case SymbolMappingMessage mapping:
            symbolMap[mapping.InstrumentId] = mapping.STypeOutSymbol;
            break;
    }
}
```

---

## 7. Schemas

| Schema | Enum | Description |
|--------|------|-------------|
| `mbo` | `Schema.Mbo` | Market by order (full book) |
| `mbp-1` | `Schema.Mbp1` | Top of book |
| `mbp-10` | `Schema.Mbp10` | 10 price levels |
| `trades` | `Schema.Trades` | All trades |
| `tbbo` | `Schema.Tbbo` | Trades with BBO |
| `ohlcv-1s` | `Schema.Ohlcv1S` | 1-second bars |
| `ohlcv-1m` | `Schema.Ohlcv1M` | 1-minute bars |
| `ohlcv-1h` | `Schema.Ohlcv1H` | 1-hour bars |
| `ohlcv-1d` | `Schema.Ohlcv1D` | Daily bars |
| `ohlcv-eod` | `Schema.OhlcvEod` | End-of-day bars |
| `definition` | `Schema.Definition` | Instrument definitions |
| `statistics` | `Schema.Statistics` | Market statistics |
| `status` | `Schema.Status` | Trading status |
| `imbalance` | `Schema.Imbalance` | Auction imbalances |
| `cmbp-1` | `Schema.Cmbp1` | Consolidated MBP-1 |
| `cbbo-1s` | `Schema.Cbbo1S` | Consolidated BBO 1s |
| `cbbo-1m` | `Schema.Cbbo1M` | Consolidated BBO 1m |
| `tcbbo` | `Schema.Tcbbo` | Trades with CBBO |
| `bbo-1s` | `Schema.Bbo1S` | BBO 1-second |
| `bbo-1m` | `Schema.Bbo1M` | BBO 1-minute |

---

## 8. Symbol Mapping

Records use numeric `InstrumentId`. Map to symbols using `SymbolMappingMessage`:

### Live Clients

```csharp
var symbolMap = new ConcurrentDictionary<uint, string>();

client.DataReceived += (s, e) =>
{
    if (e.Record is SymbolMappingMessage mapping)
        symbolMap[mapping.InstrumentId] = mapping.STypeOutSymbol;  // Use STypeOutSymbol!

    if (e.Record is TradeMessage trade)
        Console.WriteLine($"{symbolMap.GetValueOrDefault(trade.InstrumentId)}: ${trade.PriceDecimal}");
};
```

### Historical Client

Historical API doesn't send `SymbolMappingMessage`. Use `SymbologyResolveAsync()` first:

```csharp
var resolution = await client.SymbologyResolveAsync(
    dataset, symbols, SType.RawSymbol, SType.InstrumentId, startDate, endDate);

var symbolMap = new Dictionary<uint, string>();
foreach (var (symbol, intervals) in resolution.Mappings)
    foreach (var interval in intervals)
        if (uint.TryParse(interval.Symbol, out var id))
            symbolMap[id] = symbol;
```

---

## 9. DBN File I/O

### Reading

```csharp
await using var reader = DbnFileReader.Open("data.dbn");
var metadata = reader.GetMetadata();

await foreach (var record in reader.ReadRecordsAsync())
    Console.WriteLine(record);
```

### Writing

```csharp
await using var writer = DbnFileWriter.Create("output.dbn", metadata);
writer.WriteRecord(record);
writer.WriteRecords(records);
```

---

## More Information

- [API Classification](API_Classification.md) - Complete method signatures and parameters
- [Databento Docs](https://databento.com/docs/) - Official documentation
- [Issue Tracker](https://github.com/Alparse/databento-dotnet/issues)
