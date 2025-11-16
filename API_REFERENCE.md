# databento-dotnet API Reference

**Version:** v3.05-beta
**Last Updated:** November 2025

Comprehensive API reference for the databento-dotnet library, a high-performance .NET client for accessing Databento market data.

---

## Table of Contents

1. [Live Streaming API](#1-live-streaming-api)
   - [LiveClient](#liveclient)
   - [LiveClientBuilder](#liveclientbuilder)
   - [Events & Callbacks](#events--callbacks)
   - [Connection Management](#connection-management)

2. [Historical Data API](#2-historical-data-api)
   - [HistoricalClient](#historicalclient)
   - [HistoricalClientBuilder](#historicalclientbuilder)
   - [Time-Range Queries](#time-range-queries)
   - [Metadata Operations](#metadata-operations)
   - [Batch Operations](#batch-operations)
   - [Symbology Resolution](#symbology-resolution)

3. [Reference Data API](#3-reference-data-api)
   - [ReferenceClient](#referenceclient)
   - [ReferenceClientBuilder](#referenceclientbuilder)
   - [Security Master API](#security-master-api)
   - [Adjustment Factors API](#adjustment-factors-api)
   - [Corporate Actions API](#corporate-actions-api)

4. [Data Models](#4-data-models)
   - [Record Types](#record-types)
   - [Enumerations](#enumerations)
   - [Configuration Types](#configuration-types)

---

## 1. Live Streaming API

The Live Streaming API provides real-time market data streaming capabilities with async/await support, event-driven architecture, and automatic reconnection handling.

### LiveClient

The `ILiveClient` interface provides live streaming functionality for real-time market data.

#### Interface

```csharp
public interface ILiveClient : IAsyncDisposable
```

#### Creating a LiveClient

Use `LiveClientBuilder` to create instances:

```csharp
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger<ILiveClient>();

var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
await using var client = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .WithDataset("GLBX.MDP3")           // Optional: default dataset
    .WithHeartbeatInterval(TimeSpan.FromSeconds(30))
    .WithSendTsOut(false)
    .WithUpgradePolicy(VersionUpgradePolicy.Upgrade)
    .WithLogger(logger)                  // Optional: ILogger<ILiveClient>
    .WithExceptionHandler(ex =>          // Optional: ExceptionCallback
    {
        Console.WriteLine($"Stream exception: {ex.Message}");
        return ex is OperationCanceledException
            ? ExceptionAction.Stop
            : ExceptionAction.Continue;
    })
    .Build();
```

### Events & Callbacks

#### DataReceived Event

Fired when a record is received from the stream.

```csharp
event EventHandler<DataReceivedEventArgs>? DataReceived;
```

**Usage:**
```csharp
client.DataReceived += (sender, e) =>
{
    Console.WriteLine($"Received: {e.Record}");

    // Pattern match on record type
    switch (e.Record)
    {
        case TradeMessage trade:
            Console.WriteLine($"Trade: {trade.PriceDecimal} x {trade.Size}");
            break;
        case Mbp1Message mbp:
            Console.WriteLine($"MBP1: Bid={mbp.Level.BidPriceDecimal} Ask={mbp.Level.AskPriceDecimal}");
            break;
    }
};
```

#### ErrorOccurred Event

Fired when an error occurs during streaming.

```csharp
event EventHandler<ErrorEventArgs>? ErrorOccurred;
```

**Usage:**
```csharp
client.ErrorOccurred += (sender, e) =>
{
    Console.WriteLine($"Error: {e.Exception.Message}");
};
```

#### ExceptionCallback

Optional callback for handling exceptions during streaming (set via builder).

```csharp
public delegate ExceptionAction ExceptionCallback(Exception exception);

public enum ExceptionAction
{
    Continue,  // Continue processing
    Stop       // Stop the stream
}
```

**Usage:**
```csharp
.WithExceptionHandler(ex =>
{
    Console.WriteLine($"Stream error occurred: {ex.Message}");
    return ex is OperationCanceledException
        ? ExceptionAction.Stop
        : ExceptionAction.Continue;
})
```

### Subscription Methods

#### SubscribeAsync

Subscribe to a data stream with optional replay.

```csharp
Task SubscribeAsync(
    string dataset,
    Schema schema,
    IEnumerable<string> symbols,
    DateTimeOffset? startTime = null,
    CancellationToken cancellationToken = default);
```

**Parameters:**
- `dataset` - Dataset name (e.g., "GLBX.MDP3", "XNAS.ITCH", "EQUS.MINI")
- `schema` - Schema type (e.g., `Schema.Trades`, `Schema.Mbp1`, `Schema.Ohlcv1M`)
- `symbols` - List of symbols to subscribe to (e.g., `["ESH4", "NQH4"]`)
- `startTime` - Optional start time for intraday replay (null for live data)
- `cancellationToken` - Cancellation token

**Example:**
```csharp
// Live streaming
await client.SubscribeAsync(
    dataset: "EQUS.MINI",
    schema: Schema.Trades,
    symbols: new[] { "NVDA", "AAPL", "MSFT" }
);

// Intraday replay
await client.SubscribeAsync(
    dataset: "GLBX.MDP3",
    schema: Schema.Mbp1,
    symbols: new[] { "ESH4" },
    startTime: DateTimeOffset.UtcNow.AddHours(-2)
);
```

#### SubscribeWithSnapshotAsync

Subscribe to a data stream with an initial market snapshot.

```csharp
Task SubscribeWithSnapshotAsync(
    string dataset,
    Schema schema,
    IEnumerable<string> symbols,
    CancellationToken cancellationToken = default);
```

**Example:**
```csharp
await client.SubscribeWithSnapshotAsync(
    dataset: "XNAS.ITCH",
    schema: Schema.Mbp10,
    symbols: new[] { "AAPL" }
);
```

### Streaming Methods

#### StartAsync

Start receiving data and return DBN metadata.

```csharp
Task<DbnMetadata> StartAsync(CancellationToken cancellationToken = default);
```

**Returns:** `DbnMetadata` containing version, dataset, schema, and session information.

**Example:**
```csharp
var metadata = await client.StartAsync();
Console.WriteLine($"Dataset: {metadata.Dataset}");
Console.WriteLine($"Schema: {metadata.Schema}");
Console.WriteLine($"Start: {metadata.Start}");
```

#### StreamAsync

Stream records as an async enumerable.

```csharp
IAsyncEnumerable<Record> StreamAsync(CancellationToken cancellationToken = default);
```

**Example:**
```csharp
await foreach (var record in client.StreamAsync())
{
    if (record is TradeMessage trade)
    {
        Console.WriteLine($"Trade: {trade.InstrumentId} @ {trade.PriceDecimal}");
    }
}
```

#### StopAsync

Stop receiving data.

```csharp
Task StopAsync(CancellationToken cancellationToken = default);
```

**Example:**
```csharp
await client.StopAsync();
```

### Connection Management

#### ReconnectAsync

Reconnect to the gateway after disconnection.

```csharp
Task ReconnectAsync(CancellationToken cancellationToken = default);
```

#### ResubscribeAsync

Resubscribe to all previous subscriptions after reconnection.

```csharp
Task ResubscribeAsync(CancellationToken cancellationToken = default);
```

**Example:**
```csharp
try
{
    await foreach (var record in client.StreamAsync())
    {
        // Process records
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Connection lost, reconnecting... {ex.Message}");
    await client.ReconnectAsync();
    await client.ResubscribeAsync();
    await client.StartAsync();
}
```

#### BlockUntilStoppedAsync

Block until the stream stops or timeout is reached.

```csharp
// Wait indefinitely
Task BlockUntilStoppedAsync(CancellationToken cancellationToken = default);

// Wait with timeout
Task<bool> BlockUntilStoppedAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
```

**Example:**
```csharp
// Keep client alive until Ctrl+C
await client.BlockUntilStoppedAsync();

// Wait up to 5 minutes
bool stopped = await client.BlockUntilStoppedAsync(TimeSpan.FromMinutes(5));
if (!stopped)
{
    Console.WriteLine("Timeout reached, forcing stop");
    await client.StopAsync();
}
```

### LiveClientBuilder

Builder for creating `ILiveClient` instances.

#### Methods

| Method | Description |
|--------|-------------|
| `WithApiKey(string)` | **Required.** Set Databento API key |
| `WithDataset(string)` | Set default dataset for subscriptions |
| `WithSendTsOut(bool)` | Enable sending ts_out timestamps (default: false) |
| `WithUpgradePolicy(VersionUpgradePolicy)` | Set DBN version upgrade policy (default: Upgrade) |
| `WithHeartbeatInterval(TimeSpan)` | Set heartbeat interval (default: 30 seconds) |
| `WithLogger(ILogger<ILiveClient>)` | Set logger for diagnostics |
| `WithExceptionHandler(ExceptionCallback)` | Set exception handler callback |
| `Build()` | Build the LiveClient instance |

**Complete Example:**
```csharp
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

var client = new LiveClientBuilder()
    .WithApiKey(Environment.GetEnvironmentVariable("DATABENTO_API_KEY"))
    .WithDataset("GLBX.MDP3")
    .WithHeartbeatInterval(TimeSpan.FromSeconds(15))
    .WithUpgradePolicy(VersionUpgradePolicy.Upgrade)
    .WithLogger(loggerFactory.CreateLogger<ILiveClient>())
    .WithExceptionHandler(ex =>
    {
        Console.WriteLine($"Stream exception: {ex.Message}");
        return ex is OperationCanceledException
            ? ExceptionAction.Stop
            : ExceptionAction.Continue;
    })
    .Build();
```

---

## 2. Historical Data API

The Historical Data API provides time-range queries, metadata operations, batch downloads, and symbology resolution for past market data.

### HistoricalClient

The `IHistoricalClient` interface provides historical data querying capabilities.

#### Interface

```csharp
public interface IHistoricalClient : IAsyncDisposable
```

#### Creating a HistoricalClient

```csharp
var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
await using var client = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .WithGateway(HistoricalGateway.Bo1)
    .WithTimeout(TimeSpan.FromSeconds(30))
    .WithUpgradePolicy(VersionUpgradePolicy.Upgrade)
    .WithLogger(logger)
    .Build();
```

### Time-Range Queries

#### GetRangeAsync

Query historical data for a time range as an async enumerable.

```csharp
IAsyncEnumerable<Record> GetRangeAsync(
    string dataset,
    Schema schema,
    IEnumerable<string> symbols,
    DateTimeOffset startTime,
    DateTimeOffset endTime,
    CancellationToken cancellationToken = default);
```

**Example:**
```csharp
var start = new DateTimeOffset(2025, 11, 11, 0, 0, 0, TimeSpan.Zero);
var end = new DateTimeOffset(2025, 11, 12, 0, 0, 0, TimeSpan.Zero);

await foreach (var record in client.GetRangeAsync(
    dataset: "EQUS.MINI",
    schema: Schema.Trades,
    symbols: new[] { "NVDA" },
    startTime: start,
    endTime: end))
{
    if (record is TradeMessage trade)
    {
        Console.WriteLine($"{trade.Timestamp}: {trade.PriceDecimal} x {trade.Size}");
    }
}
```

#### GetRangeToFileAsync

Query historical data and save directly to a DBN file.

```csharp
Task<string> GetRangeToFileAsync(
    string filePath,
    string dataset,
    Schema schema,
    IEnumerable<string> symbols,
    DateTimeOffset startTime,
    DateTimeOffset endTime,
    CancellationToken cancellationToken = default);
```

**Example:**
```csharp
string outputPath = await client.GetRangeToFileAsync(
    filePath: "nvda_trades_20251111.dbn",
    dataset: "EQUS.MINI",
    schema: Schema.Trades,
    symbols: new[] { "NVDA" },
    startTime: start,
    endTime: end
);
Console.WriteLine($"Data saved to: {outputPath}");
```

### Metadata Operations

#### ListPublishersAsync

List all data publishers.

```csharp
Task<IReadOnlyList<PublisherDetail>> ListPublishersAsync(
    CancellationToken cancellationToken = default);
```

**Example:**
```csharp
var publishers = await client.ListPublishersAsync();
foreach (var pub in publishers)
{
    Console.WriteLine($"{pub.PublisherId}: {pub.Publisher} - {pub.Venue}");
}
```

#### ListDatasetsAsync

List available datasets, optionally filtered by venue.

```csharp
Task<IReadOnlyList<string>> ListDatasetsAsync(
    string? venue = null,
    CancellationToken cancellationToken = default);
```

**Example:**
```csharp
// All datasets
var allDatasets = await client.ListDatasetsAsync();

// CME datasets only
var cmeDatasets = await client.ListDatasetsAsync(venue: "GLBX");
```

#### ListSchemasAsync

List schemas available for a dataset.

```csharp
Task<IReadOnlyList<Schema>> ListSchemasAsync(
    string dataset,
    CancellationToken cancellationToken = default);
```

**Example:**
```csharp
var schemas = await client.ListSchemasAsync("EQUS.MINI");
foreach (var schema in schemas)
{
    Console.WriteLine($"Available: {schema.ToSchemaString()}");
}
```

#### ListFieldsAsync

List fields for a given encoding and schema.

```csharp
Task<IReadOnlyList<FieldDetail>> ListFieldsAsync(
    Encoding encoding,
    Schema schema,
    CancellationToken cancellationToken = default);
```

**Example:**
```csharp
var fields = await client.ListFieldsAsync(Encoding.Dbn, Schema.Trades);
foreach (var field in fields)
{
    Console.WriteLine($"{field.Name}: {field.TypeName}");
}
```

#### GetDatasetConditionAsync

Get dataset availability condition.

```csharp
// Current condition
Task<DatasetConditionInfo> GetDatasetConditionAsync(
    string dataset,
    CancellationToken cancellationToken = default);

// Date range condition
Task<IReadOnlyList<DatasetConditionDetail>> GetDatasetConditionAsync(
    string dataset,
    DateTimeOffset startDate,
    DateTimeOffset? endDate = null,
    CancellationToken cancellationToken = default);
```

**Example:**
```csharp
// Current status
var condition = await client.GetDatasetConditionAsync("EQUS.MINI");
Console.WriteLine($"Status: {condition.Condition}");

// Historical status
var history = await client.GetDatasetConditionAsync(
    "EQUS.MINI",
    DateTimeOffset.UtcNow.AddDays(-30),
    DateTimeOffset.UtcNow
);
```

#### GetDatasetRangeAsync

Get available time range for a dataset.

```csharp
Task<DatasetRange> GetDatasetRangeAsync(
    string dataset,
    CancellationToken cancellationToken = default);
```

**Example:**
```csharp
var range = await client.GetDatasetRangeAsync("EQUS.MINI");
Console.WriteLine($"Available from {range.StartDate} to {range.EndDate}");
```

#### Cost Estimation Methods

Get cost estimates before querying data.

```csharp
// Record count
Task<ulong> GetRecordCountAsync(
    string dataset, Schema schema,
    DateTimeOffset startTime, DateTimeOffset endTime,
    IEnumerable<string> symbols,
    CancellationToken cancellationToken = default);

// Billable size in bytes
Task<ulong> GetBillableSizeAsync(
    string dataset, Schema schema,
    DateTimeOffset startTime, DateTimeOffset endTime,
    IEnumerable<string> symbols,
    CancellationToken cancellationToken = default);

// Cost in USD
Task<decimal> GetCostAsync(
    string dataset, Schema schema,
    DateTimeOffset startTime, DateTimeOffset endTime,
    IEnumerable<string> symbols,
    CancellationToken cancellationToken = default);

// Combined billing info
Task<BillingInfo> GetBillingInfoAsync(
    string dataset, Schema schema,
    DateTimeOffset startTime, DateTimeOffset endTime,
    IEnumerable<string> symbols,
    CancellationToken cancellationToken = default);
```

**Example:**
```csharp
var billing = await client.GetBillingInfoAsync(
    dataset: "EQUS.MINI",
    schema: Schema.Trades,
    startTime: start,
    endTime: end,
    symbols: new[] { "NVDA", "AAPL" }
);

Console.WriteLine($"Records: {billing.RecordCount:N0}");
Console.WriteLine($"Size: {billing.BillableSize / (1024.0 * 1024.0):F2} MB");
Console.WriteLine($"Cost: ${billing.Cost:F2}");
```

### Batch Operations

Submit and manage bulk historical data downloads.

#### BatchSubmitJobAsync

Submit a new batch job (⚠️ **incurs cost**).

```csharp
// Simple version
Task<BatchJob> BatchSubmitJobAsync(
    string dataset,
    IEnumerable<string> symbols,
    Schema schema,
    DateTimeOffset startTime,
    DateTimeOffset endTime,
    CancellationToken cancellationToken = default);

// Advanced version with full options
Task<BatchJob> BatchSubmitJobAsync(
    string dataset,
    IEnumerable<string> symbols,
    Schema schema,
    DateTimeOffset startTime,
    DateTimeOffset endTime,
    Encoding encoding,
    Compression compression,
    bool prettyPx,
    bool prettyTs,
    bool mapSymbols,
    bool splitSymbols,
    SplitDuration splitDuration,
    ulong splitSize,
    Delivery delivery,
    SType stypeIn,
    SType stypeOut,
    ulong limit,
    CancellationToken cancellationToken = default);
```

**Example:**
```csharp
var job = await client.BatchSubmitJobAsync(
    dataset: "EQUS.MINI",
    symbols: new[] { "NVDA", "AAPL" },
    schema: Schema.Trades,
    startTime: start,
    endTime: end,
    encoding: Encoding.Csv,
    compression: Compression.Zstd,
    prettyPx: true,
    prettyTs: true,
    mapSymbols: true,
    splitSymbols: true,
    splitDuration: SplitDuration.Day,
    splitSize: 0,
    delivery: Delivery.Download,
    stypeIn: SType.RawSymbol,
    stypeOut: SType.RawSymbol,
    limit: 0
);

Console.WriteLine($"Job ID: {job.Id}");
Console.WriteLine($"State: {job.State}");
```

#### BatchListJobsAsync

List previous batch jobs.

```csharp
// All jobs
Task<IReadOnlyList<BatchJob>> BatchListJobsAsync(
    CancellationToken cancellationToken = default);

// Filtered by state and date
Task<IReadOnlyList<BatchJob>> BatchListJobsAsync(
    IEnumerable<JobState> states,
    DateTimeOffset since,
    CancellationToken cancellationToken = default);
```

**Example:**
```csharp
// Recent completed jobs
var jobs = await client.BatchListJobsAsync(
    states: new[] { JobState.Done },
    since: DateTimeOffset.UtcNow.AddDays(-7)
);
```

#### BatchListFilesAsync

List files associated with a batch job.

```csharp
Task<IReadOnlyList<BatchFileDesc>> BatchListFilesAsync(
    string jobId,
    CancellationToken cancellationToken = default);
```

#### BatchDownloadAsync

Download batch job files.

```csharp
// Download all files
Task<IReadOnlyList<string>> BatchDownloadAsync(
    string outputDir,
    string jobId,
    CancellationToken cancellationToken = default);

// Download specific file
Task<string> BatchDownloadAsync(
    string outputDir,
    string jobId,
    string filename,
    CancellationToken cancellationToken = default);
```

**Example:**
```csharp
// Download all files
var files = await client.BatchDownloadAsync("./data", job.Id);
foreach (var file in files)
{
    Console.WriteLine($"Downloaded: {file}");
}
```

### Symbology Resolution

#### SymbologyResolveAsync

Resolve symbols from one symbology type to another.

```csharp
Task<SymbologyResolution> SymbologyResolveAsync(
    string dataset,
    IEnumerable<string> symbols,
    SType stypeIn,
    SType stypeOut,
    DateOnly startDate,
    DateOnly endDate,
    CancellationToken cancellationToken = default);
```

**Example:**
```csharp
var resolution = await client.SymbologyResolveAsync(
    dataset: "EQUS.MINI",
    symbols: new[] { "NVDA" },
    stypeIn: SType.RawSymbol,
    stypeOut: SType.InstrumentId,
    startDate: new DateOnly(2025, 11, 1),
    endDate: new DateOnly(2025, 11, 30)
);

foreach (var mapping in resolution.Mappings)
{
    Console.WriteLine($"{mapping.InputSymbol} -> {mapping.Intervals.Count} mappings");
}
```

### HistoricalClientBuilder

Builder for creating `IHistoricalClient` instances.

#### Methods

| Method | Description |
|--------|-------------|
| `WithApiKey(string)` | **Required.** Set Databento API key |
| `WithGateway(HistoricalGateway)` | Set gateway (Bo1, Bo2, or Custom) |
| `WithAddress(string, ushort)` | Set custom gateway address |
| `WithUpgradePolicy(VersionUpgradePolicy)` | Set DBN version upgrade policy |
| `WithUserAgent(string)` | Extend User-Agent header |
| `WithTimeout(TimeSpan)` | Set request timeout (default: 30s) |
| `WithLogger(ILogger<IHistoricalClient>)` | Set logger for diagnostics |
| `Build()` | Build the HistoricalClient instance |

**Example:**
```csharp
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

var client = new HistoricalClientBuilder()
    .WithApiKey(Environment.GetEnvironmentVariable("DATABENTO_API_KEY"))
    .WithGateway(HistoricalGateway.Bo1)
    .WithTimeout(TimeSpan.FromMinutes(5))
    .WithUserAgent("MyApp/1.0")
    .WithLogger(loggerFactory.CreateLogger<IHistoricalClient>())
    .Build();
```

---

## 3. Reference Data API

The Reference Data API provides access to security master data, adjustment factors, and corporate actions.

### ReferenceClient

The `IReferenceClient` interface provides reference data capabilities.

#### Interface

```csharp
public interface IReferenceClient : IAsyncDisposable
{
    ISecurityMasterApi SecurityMaster { get; }
    IAdjustmentFactorsApi AdjustmentFactors { get; }
    ICorporateActionsApi CorporateActions { get; }
}
```

#### Creating a ReferenceClient

```csharp
var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
await using var client = new ReferenceClientBuilder()
    .WithApiKey(apiKey)
    .WithGateway(HistoricalGateway.Bo1)
    .WithLogger(logger)
    .Build();
```

### Security Master API

Access security master reference data.

#### GetLastAsync

Get the latest security master data.

```csharp
Task<List<SecurityMasterRecord>> GetLastAsync(
    IEnumerable<string>? symbols = null,
    SType stypeIn = SType.RawSymbol,
    IEnumerable<string>? countries = null,
    IEnumerable<string>? securityTypes = null,
    CancellationToken cancellationToken = default);
```

**Parameters:**
- `symbols` - Symbols to filter (up to 2,000), or `"ALL_SYMBOLS"`/`null` for all
- `stypeIn` - Symbology type (default: `SType.RawSymbol`)
- `countries` - ISO 3166-1 alpha-2 country codes (e.g., `["US", "GB"]`)
- `securityTypes` - Security types to filter (e.g., `["equity", "option"]`)

**Example:**
```csharp
// Specific symbols
var records = await client.SecurityMaster.GetLastAsync(
    symbols: new[] { "NVDA", "AAPL" },
    stypeIn: SType.RawSymbol
);

// All US equities
var usEquities = await client.SecurityMaster.GetLastAsync(
    countries: new[] { "US" },
    securityTypes: new[] { "equity" }
);

foreach (var record in records)
{
    Console.WriteLine($"{record.RawSymbol}: {record.SecurityType}");
    Console.WriteLine($"  ISIN: {record.Isin}");
    Console.WriteLine($"  Currency: {record.Currency}");
}
```

#### GetRangeAsync

Get security master point-in-time (PIT) time series data.

```csharp
Task<List<SecurityMasterRecord>> GetRangeAsync(
    DateTimeOffset start,
    DateTimeOffset? end = null,
    string index = "ts_effective",
    IEnumerable<string>? symbols = null,
    SType stypeIn = SType.RawSymbol,
    IEnumerable<string>? countries = null,
    IEnumerable<string>? securityTypes = null,
    CancellationToken cancellationToken = default);
```

**Parameters:**
- `start` - Inclusive start of request range
- `end` - Exclusive end (null for all data after start)
- `index` - Index column: `"ts_effective"` or `"ts_record"`
- `symbols`, `stypeIn`, `countries`, `securityTypes` - Same as GetLastAsync

**Example:**
```csharp
var history = await client.SecurityMaster.GetRangeAsync(
    start: DateTimeOffset.UtcNow.AddYears(-1),
    end: DateTimeOffset.UtcNow,
    symbols: new[] { "NVDA" }
);

Console.WriteLine($"Found {history.Count} historical records");
```

### Adjustment Factors API

Access adjustment factors for corporate actions (splits, dividends).

#### GetRangeAsync

Get adjustment factors time series data.

```csharp
Task<List<AdjustmentFactorRecord>> GetRangeAsync(
    DateTimeOffset start,
    DateTimeOffset? end = null,
    IEnumerable<string>? symbols = null,
    SType stypeIn = SType.RawSymbol,
    IEnumerable<string>? countries = null,
    IEnumerable<string>? securityTypes = null,
    CancellationToken cancellationToken = default);
```

**Example:**
```csharp
var adjustments = await client.AdjustmentFactors.GetRangeAsync(
    start: DateTimeOffset.UtcNow.AddYears(-1),
    symbols: new[] { "NVDA", "AAPL" }
);

foreach (var adj in adjustments)
{
    Console.WriteLine($"{adj.RawSymbol} on {adj.ExDate}");
    Console.WriteLine($"  Split Factor: {adj.SplitFactor}");
    Console.WriteLine($"  Dividend: {adj.Dividend}");
}
```

### Corporate Actions API

Access corporate actions data (dividends, splits, mergers, etc.).

#### GetRangeAsync

Get corporate actions time series data.

```csharp
Task<List<CorporateActionRecord>> GetRangeAsync(
    DateTimeOffset start,
    DateTimeOffset? end = null,
    string index = "event_date",
    IEnumerable<string>? symbols = null,
    SType stypeIn = SType.RawSymbol,
    IEnumerable<string>? events = null,
    IEnumerable<string>? countries = null,
    IEnumerable<string>? exchanges = null,
    IEnumerable<string>? securityTypes = null,
    bool flatten = true,
    bool pit = false,
    CancellationToken cancellationToken = default);
```

**Parameters:**
- `start`, `end` - Time range
- `index` - Index column: `"event_date"`, `"ex_date"`, or `"ts_record"`
- `symbols`, `stypeIn`, `countries`, `securityTypes` - Same as SecurityMaster
- `events` - Event types to filter (e.g., `["dividend", "split"]`)
- `exchanges` - Exchanges to filter
- `flatten` - Flatten nested JSON objects (default: true)
- `pit` - Point-in-time mode: retain all historical records (default: false)

**Example:**
```csharp
var actions = await client.CorporateActions.GetRangeAsync(
    start: DateTimeOffset.UtcNow.AddYears(-1),
    symbols: new[] { "NVDA" },
    events: new[] { "dividend", "split" }
);

foreach (var action in actions)
{
    Console.WriteLine($"{action.RawSymbol}: {action.CaType}");
    Console.WriteLine($"  Event Date: {action.EventDate}");
    Console.WriteLine($"  Ex Date: {action.ExDate}");
}
```

### ReferenceClientBuilder

Builder for creating `IReferenceClient` instances.

#### Methods

| Method | Description |
|--------|-------------|
| `WithApiKey(string)` | Set Databento API key (reads env var if not provided) |
| `WithGateway(HistoricalGateway)` | Set gateway (default: Bo1) |
| `WithLogger(ILogger<IReferenceClient>)` | Set logger for diagnostics |
| `WithHttpClient(HttpClient)` | Provide pre-configured HttpClient |
| `Build()` | Build the ReferenceClient instance |

**Example:**
```csharp
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

var client = new ReferenceClientBuilder()
    .WithApiKey(Environment.GetEnvironmentVariable("DATABENTO_API_KEY"))
    .WithGateway(HistoricalGateway.Bo1)
    .WithLogger(loggerFactory.CreateLogger<IReferenceClient>())
    .Build();
```

---

## 4. Data Models

### Record Types

All records inherit from the base `Record` class.

#### Base Record Class

```csharp
public abstract class Record
{
    public long TimestampNs { get; set; }        // Nanoseconds since Unix epoch
    public byte RType { get; set; }               // Record type identifier
    public ushort PublisherId { get; set; }       // Publisher ID
    public uint InstrumentId { get; set; }        // Instrument ID
    public DateTimeOffset Timestamp { get; }      // Computed from TimestampNs
}
```

#### Trade Message (RType: 0x00)

```csharp
public class TradeMessage : Record
{
    public long Price { get; set; }               // Fixed-point: value * 10^9
    public uint Size { get; set; }                // Trade volume
    public Action Action { get; set; }            // Trade action
    public Side Side { get; set; }                // Bid/Ask
    public byte Flags { get; set; }               // Additional flags
    public byte Depth { get; set; }               // Depth level
    public uint Sequence { get; set; }            // Sequence number

    public decimal PriceDecimal { get; }          // Helper: decimal price
}
```

**Size:** 48 bytes
**Schemas:** `Trades`, `Tbbo`

#### MBO Message (RType: 0xA0)

```csharp
public class MboMessage : Record
{
    public ulong OrderId { get; set; }            // Unique order ID
    public long Price { get; set; }               // Fixed-point: value * 10^9
    public uint Size { get; set; }                // Order size
    public byte Flags { get; set; }               // Additional flags
    public byte ChannelId { get; set; }           // Channel ID
    public Action Action { get; set; }            // Order action
    public Side Side { get; set; }                // Bid/Ask
    public long TsRecv { get; set; }              // Receive timestamp (ns)
    public int TsInDelta { get; set; }            // Delta from exchange time
    public uint Sequence { get; set; }            // Sequence number

    public decimal PriceDecimal { get; }          // Helper: decimal price
    public DateTimeOffset TsRecvTime { get; }     // Helper: receive time
}
```

**Size:** 56 bytes
**Schema:** `Mbo`

#### MBP-1 Message (RType: 0x01)

```csharp
public class Mbp1Message : Record
{
    public long Price { get; set; }               // Fixed-point: value * 10^9
    public uint Size { get; set; }                // Size at level
    public Action Action { get; set; }            // Market action
    public Side Side { get; set; }                // Bid/Ask
    public byte Flags { get; set; }               // Additional flags
    public byte Depth { get; set; }               // Depth level
    public long TsRecv { get; set; }              // Receive timestamp (ns)
    public int TsInDelta { get; set; }            // Delta from exchange time
    public uint Sequence { get; set; }            // Sequence number
    public BidAskPair Level { get; set; }         // Bid/Ask level data

    public decimal PriceDecimal { get; }          // Helper: decimal price
    public DateTimeOffset TsRecvTime { get; }     // Helper: receive time
}
```

**Size:** 80 bytes
**Schema:** `Mbp1`, `Tbbo`

#### MBP-10 Message (RType: 0x0A)

```csharp
public class Mbp10Message : Record
{
    public long Price { get; set; }
    public uint Size { get; set; }
    public Action Action { get; set; }
    public Side Side { get; set; }
    public byte Flags { get; set; }
    public byte Depth { get; set; }
    public long TsRecv { get; set; }
    public int TsInDelta { get; set; }
    public uint Sequence { get; set; }
    public BidAskPair[] Levels { get; set; }      // 10 levels

    public decimal PriceDecimal { get; }
    public DateTimeOffset TsRecvTime { get; }
}
```

**Size:** 368 bytes
**Schema:** `Mbp10`

#### OHLCV Message (RTypes: 0x11, 0x20-0x24)

```csharp
public class OhlcvMessage : Record
{
    public long Open { get; set; }                // Fixed-point
    public long High { get; set; }                // Fixed-point
    public long Low { get; set; }                 // Fixed-point
    public long Close { get; set; }               // Fixed-point
    public ulong Volume { get; set; }             // Total volume

    public decimal OpenDecimal { get; }           // Helpers
    public decimal HighDecimal { get; }
    public decimal LowDecimal { get; }
    public decimal CloseDecimal { get; }
}
```

**Size:** 56 bytes
**Schemas:** `Ohlcv1S`, `Ohlcv1M`, `Ohlcv1H`, `Ohlcv1D`, `OhlcvEod`

#### Status Message (RType: 0x12)

```csharp
public class StatusMessage : Record
{
    public long TsRecv { get; set; }              // Receive timestamp
    public StatusAction Action { get; set; }      // Trading status action
    public StatusReason Reason { get; set; }      // Status reason
    public TradingEvent TradingEvent { get; set; }// Trading event
    public TriState IsTrading { get; set; }       // Trading flag
    public TriState IsQuoting { get; set; }       // Quoting flag
    public TriState IsShortSellRestricted { get; set; } // SSR flag

    public DateTimeOffset TsRecvTime { get; }     // Helper: receive time
}
```

**Size:** 40 bytes
**Schema:** `Status`

#### Instrument Definition Message (RType: 0x13)

```csharp
public class InstrumentDefMessage : Record
{
    public long TsRecv { get; set; }
    public long MinPriceIncrement { get; set; }   // Tick size
    public long DisplayFactor { get; set; }       // Display factor
    public long Expiration { get; set; }          // Expiration timestamp
    public long Activation { get; set; }          // Activation timestamp
    public long HighLimitPrice { get; set; }
    public long LowLimitPrice { get; set; }
    public long MaxPriceVariation { get; set; }
    public long TradingReferencePrice { get; set; }
    public long StrikePrice { get; set; }         // For options

    // String fields
    public string Currency { get; set; }          // Currency code
    public string SettlCurrency { get; set; }     // Settlement currency
    public string SecSubType { get; set; }        // Security subtype
    public string RawSymbol { get; set; }         // Raw symbol
    public string Group { get; set; }             // Security group
    public string Exchange { get; set; }          // Exchange code
    public string Asset { get; set; }             // Asset code
    public string Cfi { get; set; }               // CFI code
    public string SecurityType { get; set; }      // Security type
    public string UnitOfMeasure { get; set; }     // Unit of measure
    public string Underlying { get; set; }        // Underlying symbol

    // Enums
    public InstrumentClass InstrumentClass { get; set; }
    public MatchAlgorithm MatchAlgorithm { get; set; }

    // Helpers for decimal prices
    public decimal MinPriceIncrementDecimal { get; }
    public decimal StrikePriceDecimal { get; }
}
```

**Size:** 520 bytes
**Schema:** `Definition`

#### Imbalance Message (RType: 0x14)

```csharp
public class ImbalanceMessage : Record
{
    public long TsRecv { get; set; }
    public long RefPrice { get; set; }            // Reference price
    public long AuctionTime { get; set; }         // Auction timestamp
    public ulong PairedQty { get; set; }          // Paired quantity
    public ulong TotalImbalanceQty { get; set; }  // Total imbalance
    public Side Side { get; set; }                // Imbalance side

    public decimal RefPriceDecimal { get; }
    public DateTimeOffset AuctionTimeOffset { get; }
}
```

**Size:** 112 bytes
**Schema:** `Imbalance`

#### Statistics Message (RType: 0x18)

```csharp
public class StatMessage : Record
{
    public long TsRecv { get; set; }
    public long TsRef { get; set; }               // Reference timestamp
    public long Price { get; set; }               // Statistic price
    public long Quantity { get; set; }            // Statistic quantity
    public uint Sequence { get; set; }
    public int TsInDelta { get; set; }
    public ushort StatType { get; set; }          // Statistic type code
    public ushort ChannelId { get; set; }
    public byte UpdateAction { get; set; }
    public byte StatFlags { get; set; }

    public decimal PriceDecimal { get; }
}
```

**Size:** 80 bytes
**Schema:** `Statistics`

#### BBO Message (RTypes: 0xC3, 0xC4)

```csharp
public class BboMessage : Record
{
    public long Price { get; set; }
    public uint Size { get; set; }
    public Side Side { get; set; }
    public byte Flags { get; set; }
    public long TsRecv { get; set; }
    public uint Sequence { get; set; }
    public BidAskPair Level { get; set; }

    public decimal PriceDecimal { get; }
    public DateTimeOffset TsRecvTime { get; }
}
```

**Size:** 80 bytes
**Schemas:** `Bbo1S`, `Bbo1M`

#### CBBO Message (RTypes: 0xC0, 0xC1)

```csharp
public class CbboMessage : Record
{
    public long Price { get; set; }
    public uint Size { get; set; }
    public Side Side { get; set; }
    public byte Flags { get; set; }
    public long TsRecv { get; set; }
    public uint Sequence { get; set; }
    public ConsolidatedBidAskPair Level { get; set; }

    public decimal PriceDecimal { get; }
}
```

**Size:** 80 bytes
**Schemas:** `Cbbo1S`, `Cbbo1M`

#### CMBP-1 Message (RType: 0xB1)

```csharp
public class Cmbp1Message : Record
{
    public long Price { get; set; }
    public uint Size { get; set; }
    public Action Action { get; set; }
    public Side Side { get; set; }
    public byte Flags { get; set; }
    public long TsRecv { get; set; }
    public int TsInDelta { get; set; }
    public ConsolidatedBidAskPair Level { get; set; }

    public decimal PriceDecimal { get; }
}
```

**Size:** 80 bytes
**Schema:** `Cmbp1`

#### TCBBO Message (RType: 0xC2)

```csharp
public class TcbboMessage : Record
{
    public long Price { get; set; }
    public uint Size { get; set; }
    public Action Action { get; set; }
    public Side Side { get; set; }
    public byte Flags { get; set; }
    public long TsRecv { get; set; }
    public int TsInDelta { get; set; }
    public ConsolidatedBidAskPair Level { get; set; }

    public decimal PriceDecimal { get; }
}
```

**Size:** 80 bytes
**Schema:** `Tcbbo`

#### Error Message (RType: 0x15)

```csharp
public class ErrorMessage : Record
{
    public string Error { get; set; }             // Error message text
    public ErrorCode Code { get; set; }           // Error code
    public bool IsLast { get; set; }              // Last error in sequence
}
```

**Size:** 320 bytes

#### System Message (RType: 0x17)

```csharp
public class SystemMessage : Record
{
    public string Message { get; set; }           // System message text
    public SystemCode Code { get; set; }          // System message code
}
```

**Size:** 320 bytes

#### Symbol Mapping Message (RType: 0x16)

```csharp
public class SymbolMappingMessage : Record
{
    public SType STypeIn { get; set; }            // Input symbology type
    public string STypeInSymbol { get; set; }     // Input symbol
    public SType STypeOut { get; set; }           // Output symbology type
    public string STypeOutSymbol { get; set; }    // Output symbol
    public long StartTs { get; set; }             // Mapping start time (ns)
    public long EndTs { get; set; }               // Mapping end time (ns)

    public DateTimeOffset StartTime { get; }
    public DateTimeOffset EndTime { get; }
}
```

**Size:** 176 bytes

#### Helper Data Structures

**BidAskPair** (32 bytes):
```csharp
public struct BidAskPair
{
    public long BidPrice { get; set; }            // Fixed-point
    public long AskPrice { get; set; }            // Fixed-point
    public uint BidSize { get; set; }
    public uint AskSize { get; set; }
    public uint BidCount { get; set; }            // Order count
    public uint AskCount { get; set; }            // Order count

    public decimal BidPriceDecimal { get; }
    public decimal AskPriceDecimal { get; }
}
```

**ConsolidatedBidAskPair** (32 bytes):
```csharp
public struct ConsolidatedBidAskPair
{
    public long BidPrice { get; set; }            // Fixed-point
    public long AskPrice { get; set; }            // Fixed-point
    public uint BidSize { get; set; }
    public uint AskSize { get; set; }
    public ushort BidPublisher { get; set; }      // Publisher ID
    public ushort AskPublisher { get; set; }      // Publisher ID

    public decimal BidPriceDecimal { get; }
    public decimal AskPriceDecimal { get; }
}
```

### Enumerations

#### Schema

Market data schema types.

```csharp
public enum Schema : ushort
{
    Mbo,                // Market by order
    Mbp1,               // Market by price level 1
    Mbp10,              // Market by price level 10
    Tbbo,               // Trade with BBO
    Trades,             // All trades
    Ohlcv1S,            // OHLCV 1-second bars
    Ohlcv1M,            // OHLCV 1-minute bars
    Ohlcv1H,            // OHLCV 1-hour bars
    Ohlcv1D,            // OHLCV 1-day bars
    Definition,         // Instrument definitions
    Statistics,         // Market statistics
    Status,             // Trading status
    Imbalance,          // Auction imbalances
    OhlcvEod,           // OHLCV end-of-day bars
    Cmbp1,              // Consolidated MBP-1
    Cbbo1S,             // Consolidated BBO 1-second
    Cbbo1M,             // Consolidated BBO 1-minute
    Tcbbo,              // Trade with consolidated BBO
    Bbo1S,              // BBO 1-second
    Bbo1M               // BBO 1-minute
}
```

**String Conversion:**
```csharp
var schemaStr = Schema.Trades.ToSchemaString();  // "trades"
var schema = SchemaExtensions.ParseSchema("mbp-1"); // Schema.Mbp1
```

#### RType

Record type identifiers (byte codes).

```csharp
public enum RType : byte
{
    Mbp0 = 0x00,              // Trade messages
    Mbp1 = 0x01,              // MBP Level 1
    Mbp10 = 0x0A,             // MBP Level 10
    OhlcvDeprecated = 0x11,   // Deprecated OHLCV
    Status = 0x12,            // Trading status
    InstrumentDef = 0x13,     // Instrument definitions
    Imbalance = 0x14,         // Order imbalances
    Error = 0x15,             // Error messages
    SymbolMapping = 0x16,     // Symbol mappings
    System = 0x17,            // System messages
    Statistics = 0x18,        // Market statistics
    Ohlcv1S = 0x20,           // OHLCV 1-second
    Ohlcv1M = 0x21,           // OHLCV 1-minute
    Ohlcv1H = 0x22,           // OHLCV 1-hour
    Ohlcv1D = 0x23,           // OHLCV 1-day
    OhlcvEod = 0x24,          // OHLCV EOD
    Mbo = 0xA0,               // Market by Order
    Cmbp1 = 0xB1,             // Consolidated MBP-1
    Cbbo1S = 0xC0,            // Consolidated BBO 1-second
    Cbbo1M = 0xC1,            // Consolidated BBO 1-minute
    Tcbbo = 0xC2,             // Trade with CBBO
    Bbo1S = 0xC3,             // BBO 1-second
    Bbo1M = 0xC4              // BBO 1-minute
}
```

#### SType (Symbology Type)

```csharp
public enum SType : byte
{
    InstrumentId,       // Databento instrument ID
    RawSymbol,          // Native exchange symbol
    Smart,              // Smart symbol resolution
    Continuous,         // Continuous futures
    Parent,             // Parent symbol
    NasdaqSymbol,       // Nasdaq symbol
    CmsSymbol,          // CMS symbol
    Isin,               // ISIN code
    UsCode,             // US code
    BbgCompId,          // Bloomberg composite ID
    BbgCompTicker,      // Bloomberg composite ticker
    Figi,               // FIGI code
    FigiTicker          // FIGI ticker
}
```

**String Conversion:**
```csharp
var stypeStr = SType.RawSymbol.ToStypeString();  // "raw_symbol"
```

#### Action

```csharp
public enum Action : byte
{
    Modify = (byte)'M',    // Order modification
    Trade = (byte)'T',     // Trade execution
    Fill = (byte)'F',      // Order fill
    Cancel = (byte)'C',    // Order cancellation
    Add = (byte)'A',       // Order addition
    Clear = (byte)'R',     // Clear/reset
    None = (byte)'N'       // No action
}
```

#### Side

```csharp
public enum Side : byte
{
    Ask = (byte)'A',       // Ask/Offer side
    Bid = (byte)'B',       // Bid side
    None = (byte)'N'       // No side
}
```

#### InstrumentClass

```csharp
public enum InstrumentClass : byte
{
    Bond = (byte)'B',
    Call = (byte)'C',
    Future = (byte)'F',
    Stock = (byte)'K',
    MixedSpread = (byte)'M',
    Put = (byte)'P',
    FutureSpread = (byte)'S',
    OptionSpread = (byte)'T',
    FxSpot = (byte)'X',
    CommoditySpot = (byte)'Y'
}
```

#### StatusAction

Trading status actions.

```csharp
public enum StatusAction : byte
{
    None = 0,
    PreOpen = 1,
    PreCross = 2,
    Quoting = 3,
    Cross = 4,
    Rotation = 5,
    NewPriceIndication = 6,
    Trading = 7,
    Halt = 8,
    Pause = 9,
    Suspend = 10,
    PreClose = 11,
    Close = 12,
    PostClose = 13,
    Closed = 14,
    PrivateAuction = 200
}
```

#### ErrorCode

```csharp
public enum ErrorCode : byte
{
    AuthFailed = 1,                  // Authentication failed
    ApiKeyDeactivated = 2,           // API key deactivated
    ConnectionLimitExceeded = 3,     // Too many connections
    SymbolResolutionFailed = 4,      // Symbol lookup failed
    InvalidSubscription = 5,         // Invalid subscription
    InternalError = 6,               // Gateway error
    Unset = 255                      // No error code
}
```

#### SystemCode

```csharp
public enum SystemCode : byte
{
    Heartbeat = 0,              // Heartbeat message
    SubscriptionAck = 1,        // Subscription acknowledged
    SlowReaderWarning = 2,      // Session falling behind
    ReplayCompleted = 3,        // Replay caught up to real-time
    EndOfInterval = 4,          // End of interval marker
    Unset = 255                 // No system code
}
```

### Configuration Types

#### HistoricalGateway

```csharp
public enum HistoricalGateway
{
    Bo1,        // Primary gateway (bo1.databento.com)
    Bo2,        // Secondary gateway (bo2.databento.com)
    Custom      // Custom address
}
```

#### VersionUpgradePolicy

```csharp
public enum VersionUpgradePolicy
{
    AsIs,       // Keep original DBN version
    Upgrade     // Upgrade to latest DBN version
}
```

#### Encoding

```csharp
public enum Encoding
{
    Dbn,        // Databento Binary Encoding
    Csv,        // Comma-separated values
    Json        // JSON format
}
```

#### Compression

```csharp
public enum Compression
{
    None,       // No compression
    Zstd,       // Zstandard compression
    Gzip        // Gzip compression
}
```

#### SplitDuration

For batch job file splitting.

```csharp
public enum SplitDuration
{
    None,       // Single file
    Day,        // Split by day
    Week,       // Split by week
    Month       // Split by month
}
```

#### JobState

Batch job processing states.

```csharp
public enum JobState
{
    Received,       // Job received
    Queued,         // Job queued
    Processing,     // Job processing
    Done,           // Job completed
    Expired         // Job expired
}
```

#### DatasetCondition

```csharp
public enum DatasetCondition
{
    Available,      // Dataset available
    Degraded,       // Degraded availability
    Pending,        // Availability pending
    Missing         // Dataset missing
}
```

---

## Complete Usage Examples

### Live Streaming with Error Handling

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;
using Microsoft.Extensions.Logging;

var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

await using var client = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .WithLogger(loggerFactory.CreateLogger<ILiveClient>())
    .WithExceptionHandler(ex =>
    {
        Console.WriteLine($"Stream exception: {ex.Message}");
        return ex is OperationCanceledException
            ? ExceptionAction.Stop
            : ExceptionAction.Continue;
    })
    .Build();

// Subscribe to multiple schemas
await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA" });
await client.SubscribeAsync("EQUS.MINI", Schema.Mbp1, new[] { "NVDA" });

// Event-driven processing
client.DataReceived += (sender, e) =>
{
    switch (e.Record)
    {
        case TradeMessage trade:
            Console.WriteLine($"Trade: {trade.PriceDecimal:F2} x {trade.Size}");
            break;
        case Mbp1Message mbp:
            var spread = mbp.Level.AskPriceDecimal - mbp.Level.BidPriceDecimal;
            Console.WriteLine($"Spread: {spread:F4}");
            break;
    }
};

// Start streaming
await client.StartAsync();
await client.BlockUntilStoppedAsync();
```

### Historical Data with Cost Estimation

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
await using var client = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .WithTimeout(TimeSpan.FromMinutes(5))
    .Build();

var start = new DateTimeOffset(2025, 11, 1, 0, 0, 0, TimeSpan.Zero);
var end = new DateTimeOffset(2025, 11, 2, 0, 0, 0, TimeSpan.Zero);
var symbols = new[] { "NVDA", "AAPL", "MSFT" };

// Check cost before querying
var billing = await client.GetBillingInfoAsync(
    "EQUS.MINI", Schema.Trades, start, end, symbols
);

Console.WriteLine($"Query will cost ${billing.Cost:F2}");
Console.WriteLine($"Records: {billing.RecordCount:N0}");
Console.WriteLine($"Size: {billing.BillableSize / 1024.0 / 1024.0:F2} MB");

// Proceed with query
await foreach (var record in client.GetRangeAsync(
    "EQUS.MINI", Schema.Trades, symbols, start, end))
{
    if (record is TradeMessage trade)
    {
        Console.WriteLine($"{trade.Timestamp:HH:mm:ss.fff}: {trade.PriceDecimal:F2}");
    }
}
```

### Reference Data Analysis

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
await using var client = new ReferenceClientBuilder()
    .WithApiKey(apiKey)
    .Build();

var symbols = new[] { "NVDA", "AAPL" };

// Get current security master data
var securities = await client.SecurityMaster.GetLastAsync(symbols);
foreach (var sec in securities)
{
    Console.WriteLine($"\n{sec.RawSymbol}:");
    Console.WriteLine($"  ISIN: {sec.Isin}");
    Console.WriteLine($"  Type: {sec.SecurityType}");
    Console.WriteLine($"  Currency: {sec.Currency}");
}

// Get recent corporate actions
var actions = await client.CorporateActions.GetRangeAsync(
    start: DateTimeOffset.UtcNow.AddYears(-1),
    symbols: symbols,
    events: new[] { "dividend", "split" }
);

Console.WriteLine($"\nCorporate Actions:");
foreach (var action in actions)
{
    Console.WriteLine($"{action.EventDate:yyyy-MM-dd}: {action.RawSymbol} - {action.CaType}");
}

// Get adjustment factors for backtesting
var adjustments = await client.AdjustmentFactors.GetRangeAsync(
    start: DateTimeOffset.UtcNow.AddYears(-1),
    symbols: symbols
);

Console.WriteLine($"\nAdjustment Factors:");
foreach (var adj in adjustments)
{
    Console.WriteLine($"{adj.ExDate:yyyy-MM-dd}: {adj.RawSymbol}");
    Console.WriteLine($"  Split: {adj.SplitFactor:F4}");
    Console.WriteLine($"  Dividend: {adj.Dividend:F4}");
}
```

---

## Error Handling

### Common Error Patterns

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

try
{
    await using var client = new LiveClientBuilder()
        .WithApiKey("invalid-key")
        .Build();

    await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA" });
    await client.StartAsync();
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Configuration error: {ex.Message}");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"Network error: {ex.Message}");
}
catch (TimeoutException ex)
{
    Console.WriteLine($"Request timeout: {ex.Message}");
}
```

### Error Messages in Stream

```csharp
client.DataReceived += (sender, e) =>
{
    if (e.Record is ErrorMessage error)
    {
        Console.WriteLine($"Error Code: {error.Code}");
        Console.WriteLine($"Message: {error.Error}");

        switch (error.Code)
        {
            case ErrorCode.AuthFailed:
                Console.WriteLine("Check your API key");
                break;
            case ErrorCode.SymbolResolutionFailed:
                Console.WriteLine("Invalid symbol requested");
                break;
        }

        if (error.IsLast)
        {
            Console.WriteLine("Final error, stream may stop");
        }
    }
};
```

---

## Best Practices

### 1. Always Use Environment Variables for API Keys

```csharp
// ✅ GOOD
var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
    ?? throw new InvalidOperationException("DATABENTO_API_KEY not set");

// ❌ BAD - Never hardcode
var apiKey = "db-1234567890abcdef";
```

### 2. Use `await using` for Proper Disposal

```csharp
// ✅ GOOD
await using var client = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .Build();

// ❌ BAD - May leak resources
var client = new LiveClientBuilder().WithApiKey(apiKey).Build();
```

### 3. Enable Logging in Production

```csharp
using Microsoft.Extensions.Logging;

// ✅ GOOD
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .AddDebug()
        .SetMinimumLevel(LogLevel.Information);
});

var client = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .WithLogger(loggerFactory.CreateLogger<ILiveClient>())
    .Build();
```

### 4. Check Costs Before Historical Queries

```csharp
// ✅ GOOD - Check cost first
var cost = await client.GetCostAsync(dataset, schema, start, end, symbols);
if (cost > 10.0m)
{
    Console.WriteLine($"Query costs ${cost:F2}, proceed? (y/n)");
    if (Console.ReadLine() != "y") return;
}

await foreach (var record in client.GetRangeAsync(...))
{
    // Process
}
```

### 5. Handle Reconnections Gracefully

```csharp
// ✅ GOOD
bool shouldReconnect = true;
while (shouldReconnect)
{
    try
    {
        await foreach (var record in client.StreamAsync())
        {
            // Process
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Connection lost, reconnecting... {ex.Message}");
        await Task.Delay(TimeSpan.FromSeconds(5));
        await client.ReconnectAsync();
        await client.ResubscribeAsync();
        await client.StartAsync();
    }
}
```

### 6. Use Pattern Matching for Record Types

```csharp
// ✅ GOOD
await foreach (var record in client.StreamAsync())
{
    switch (record)
    {
        case TradeMessage trade:
            ProcessTrade(trade);
            break;
        case Mbp1Message mbp:
            ProcessQuote(mbp);
            break;
        case ErrorMessage error:
            HandleError(error);
            break;
    }
}
```

---

## Performance Considerations

1. **Memory Management**: Records are copied from native memory. For high-throughput scenarios, consider batching.

2. **Threading**: Event callbacks fire on the thread pool. Heavy processing should be offloaded.

3. **Backpressure**: The internal `Channel<T>` is unbounded by default. Monitor memory usage in production.

4. **Fixed-Point Arithmetic**: All prices use fixed-point (multiply by 10^9). Use decimal helpers for display.

---

## Additional Resources

- **Official Documentation**: https://docs.databento.com
- **GitHub Repository**: https://github.com/Alparse/databento-dotnet
- **Issue Tracker**: https://github.com/Alparse/databento-dotnet/issues
- **Databento API Portal**: https://databento.com/portal

---

**Document Version:** 1.0
**Library Version:** v3.05-beta
**Last Updated:** November 2025
