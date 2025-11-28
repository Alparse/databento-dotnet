# Databento .NET Client - Complete API Classification

**Version:** 4.0.1-beta
**Generated:** 2025-11-27

This document provides a comprehensive classification of every API call available in the databento-dotnet library, including input parameters and expected outputs. This library wraps databento-cpp (Databento's C++ client library) for .NET consumption.

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Client APIs](#2-client-apis)
   - [HistoricalClient](#21-historicalclient)
   - [LiveClient](#22-liveclient)
   - [LiveBlockingClient](#23-liveblockingclient)
3. [Data Models](#3-data-models)
   - [Record Types](#31-record-types)
   - [Supporting Structures](#32-supporting-structures)
4. [Enumerations](#4-enumerations)
5. [Builder Classes](#5-builder-classes)
   - [ReferenceClient](#51-referenceclient)
6. [Symbol Mapping APIs](#6-symbol-mapping-apis)
7. [DBN File I/O](#7-dbn-file-io)
8. [Native C API Reference](#8-native-c-api-reference)

---

## 1. Architecture Overview

The library follows a layered architecture:

```
┌─────────────────────────────────────────────────────────────┐
│  Application Code (C#)                                      │
│  Uses: HistoricalClient, LiveClient, LiveBlockingClient     │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  Databento.Client (.NET 8.0)                                │
│  High-level managed API with IAsyncEnumerable support       │
│  83 C# files providing:                                     │
│  - Client classes (Historical, Live, LiveBlocking)          │
│  - 16 DBN record types                                      │
│  - Fluent builders                                          │
│  - Symbol mapping utilities                                 │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  Databento.Interop (.NET 8.0)                               │
│  P/Invoke layer with SafeHandle wrappers                    │
│  - NativeMethods (P/Invoke declarations)                    │
│  - 9 SafeHandle wrapper classes                             │
│  - Native library loader                                    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  databento_native.dll (C++ Wrapper - 3,879 lines)           │
│  C ABI wrapper around databento-cpp                         │
│  - live_client_wrapper.cpp (699 lines)                      │
│  - live_blocking_wrapper.cpp (514 lines)                    │
│  - historical_client_wrapper.cpp (1,559 lines)              │
│  - symbol_map_wrapper.cpp (293 lines)                       │
│  - dbn_file_reader_wrapper.cpp (230 lines)                  │
│  - dbn_file_writer_wrapper.cpp (200 lines)                  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  databento-cpp (Upstream C++ Library - Submodule)           │
│  Core functionality: network, parsing, serialization        │
│  Dependencies: OpenSSL, zstd, zlib, nlohmann/json, httplib  │
└─────────────────────────────────────────────────────────────┘
```

**Supported Platforms:**
- Windows x64 (primary)
- Linux x64 & arm64
- macOS x64 & arm64

---

## 2. Client APIs

### 2.1 HistoricalClient

**Interface:** `IHistoricalClient`
**Implementation:** `HistoricalClient`
**Purpose:** Query historical market data with time range filtering
**Lifecycle:** `IAsyncDisposable`

#### 2.1.1 Construction via Builder

```csharp
var client = new HistoricalClientBuilder()
    .WithApiKey(apiKey)           // Required - Databento API key
    .WithGateway(gateway)         // Optional - Bo1 (default), Bo2, Custom
    .WithAddress(host, port)      // Optional - Custom gateway (requires Custom gateway)
    .WithUpgradePolicy(policy)    // Optional - AsIs or Upgrade (default)
    .WithUserAgent(userAgent)     // Optional - Additional user agent string
    .WithTimeout(timeout)         // Optional - Request timeout (default: 30s)
    .WithLogger(logger)           // Optional - ILogger<IHistoricalClient>
    .Build();
```

#### 2.1.2 Time-Range Query Methods

| Method | Input Parameters | Output | Description |
|--------|------------------|--------|-------------|
| `GetRangeAsync` | `dataset`: string<br>`schema`: Schema<br>`symbols`: IEnumerable&lt;string&gt;<br>`startTime`: DateTimeOffset<br>`endTime`: DateTimeOffset<br>`cancellationToken`: CancellationToken = default | `IAsyncEnumerable<Record>` | Query historical data for a time range |
| `GetRangeAsync` (with symbology) | Above + <br>`stypeIn`: SType<br>`stypeOut`: SType<br>`limit`: ulong = 0 | `IAsyncEnumerable<Record>` | Query with symbology type filtering |
| `GetRangeToFileAsync` | `filePath`: string<br>`dataset`: string<br>`schema`: Schema<br>`symbols`: IEnumerable&lt;string&gt;<br>`startTime`: DateTimeOffset<br>`endTime`: DateTimeOffset<br>`cancellationToken`: CancellationToken = default | `Task<string>` | Query and save directly to DBN file. Returns file path. |
| `GetRangeToFileAsync` (with symbology) | Above + <br>`stypeIn`: SType<br>`stypeOut`: SType<br>`limit`: ulong = 0 | `Task<string>` | Query with symbology and save to file |

#### 2.1.3 Metadata Query Methods

| Method | Input Parameters | Output | Description |
|--------|------------------|--------|-------------|
| `GetMetadata` | `dataset`: string<br>`schema`: Schema<br>`startTime`: DateTimeOffset<br>`endTime`: DateTimeOffset | `IMetadata?` | Get metadata for a query |
| `ListPublishersAsync` | `cancellationToken`: CancellationToken = default | `Task<IReadOnlyList<PublisherDetail>>` | List all data publishers |
| `ListDatasetsAsync` | `venue`: string? = null<br>`cancellationToken`: CancellationToken = default | `Task<IReadOnlyList<string>>` | List datasets, optionally filtered by venue |
| `ListSchemasAsync` | `dataset`: string<br>`cancellationToken`: CancellationToken = default | `Task<IReadOnlyList<Schema>>` | List schemas available for dataset |
| `ListFieldsAsync` | `encoding`: Encoding<br>`schema`: Schema<br>`cancellationToken`: CancellationToken = default | `Task<IReadOnlyList<FieldDetail>>` | List fields for encoding/schema combination |
| `GetDatasetConditionAsync` | `dataset`: string<br>`cancellationToken`: CancellationToken = default | `Task<DatasetConditionInfo>` | Get dataset availability condition |
| `GetDatasetConditionAsync` (date range) | `dataset`: string<br>`startDate`: DateTimeOffset<br>`endDate`: DateTimeOffset? = null<br>`cancellationToken`: CancellationToken = default | `Task<IReadOnlyList<DatasetConditionDetail>>` | Get condition for date range |
| `GetDatasetRangeAsync` | `dataset`: string<br>`cancellationToken`: CancellationToken = default | `Task<DatasetRange>` | Get dataset time range |
| `ListUnitPricesAsync` | `dataset`: string<br>`cancellationToken`: CancellationToken = default | `Task<IReadOnlyList<UnitPricesForMode>>` | List unit prices per schema |

#### 2.1.4 Billing Query Methods

| Method | Input Parameters | Output | Description |
|--------|------------------|--------|-------------|
| `GetRecordCountAsync` | `dataset`: string<br>`schema`: Schema<br>`startTime`: DateTimeOffset<br>`endTime`: DateTimeOffset<br>`symbols`: IEnumerable&lt;string&gt;<br>`cancellationToken`: CancellationToken = default | `Task<ulong>` | Get estimated record count for query |
| `GetBillableSizeAsync` | Same as above | `Task<ulong>` | Get billable size in bytes |
| `GetCostAsync` | Same as above | `Task<decimal>` | Get cost estimate in USD |
| `GetBillingInfoAsync` | Same as above | `Task<BillingInfo>` | Get combined billing information |

#### 2.1.5 Batch Job Methods

| Method | Input Parameters | Output | Description |
|--------|------------------|--------|-------------|
| `BatchSubmitJobAsync` | `dataset`: string<br>`symbols`: IEnumerable&lt;string&gt;<br>`schema`: Schema<br>`startTime`: DateTimeOffset<br>`endTime`: DateTimeOffset<br>`cancellationToken`: CancellationToken = default | `Task<BatchJob>` | Submit batch job (basic). **WARNING: Incurs cost** |
| `BatchSubmitJobAsync` (advanced) | Above + <br>`encoding`: Encoding<br>`compression`: Compression<br>`prettyPx`: bool<br>`prettyTs`: bool<br>`mapSymbols`: bool<br>`splitSymbols`: bool<br>`splitDuration`: SplitDuration<br>`splitSize`: ulong<br>`delivery`: Delivery<br>`stypeIn`: SType<br>`stypeOut`: SType<br>`limit`: ulong | `Task<BatchJob>` | Submit batch job with all options |
| `BatchListJobsAsync` | `cancellationToken`: CancellationToken = default | `Task<IReadOnlyList<BatchJob>>` | List all batch jobs |
| `BatchListJobsAsync` (filtered) | `states`: IEnumerable&lt;JobState&gt;<br>`since`: DateTimeOffset<br>`cancellationToken`: CancellationToken = default | `Task<IReadOnlyList<BatchJob>>` | List filtered batch jobs |
| `BatchListFilesAsync` | `jobId`: string<br>`cancellationToken`: CancellationToken = default | `Task<IReadOnlyList<BatchFileDesc>>` | List files for batch job |
| `BatchDownloadAsync` | `outputDir`: string<br>`jobId`: string<br>`cancellationToken`: CancellationToken = default | `Task<IReadOnlyList<string>>` | Download all files from job |
| `BatchDownloadAsync` (single) | `outputDir`: string<br>`jobId`: string<br>`filename`: string<br>`cancellationToken`: CancellationToken = default | `Task<string>` | Download specific file |

#### 2.1.6 Symbology Resolution Methods

| Method | Input Parameters | Output | Description |
|--------|------------------|--------|-------------|
| `SymbologyResolveAsync` | `dataset`: string<br>`symbols`: IEnumerable&lt;string&gt;<br>`stypeIn`: SType<br>`stypeOut`: SType<br>`startDate`: DateOnly<br>`endDate`: DateOnly<br>`cancellationToken`: CancellationToken = default | `Task<SymbologyResolution>` | Resolve symbols between symbology types |

---

### 2.2 LiveClient

**Interface:** `ILiveClient`
**Implementation:** `LiveClient`
**Purpose:** Async streaming client for real-time market data (push-based, IAsyncEnumerable)
**Lifecycle:** `IAsyncDisposable`

#### 2.2.1 Construction via Builder

```csharp
var client = new LiveClientBuilder()
    .WithApiKey(apiKey)                    // Required - Databento API key
    .WithDataset(dataset)                  // Optional - Default dataset for subscriptions
    .WithSendTsOut(sendTsOut)              // Optional - Include ts_out timestamps (default: false)
    .WithUpgradePolicy(policy)             // Optional - AsIs or Upgrade (default)
    .WithHeartbeatInterval(interval)       // Optional - Connection heartbeat (default: 30s)
    .WithLogger(logger)                    // Optional - ILogger<ILiveClient>
    .WithExceptionHandler(handler)         // Optional - ExceptionCallback for error handling
    .Build();
```

#### 2.2.2 Events

| Event | Event Args Type | Description |
|-------|-----------------|-------------|
| `DataReceived` | `DataReceivedEventArgs` | Fired when data is received |
| `ErrorOccurred` | `ErrorEventArgs` | Fired when an error occurs |

#### 2.2.3 Subscription Methods

| Method | Input Parameters | Output | Description |
|--------|------------------|--------|-------------|
| `SubscribeAsync` | `dataset`: string<br>`schema`: Schema<br>`symbols`: IEnumerable&lt;string&gt;<br>`startTime`: DateTimeOffset? = null<br>`cancellationToken`: CancellationToken = default | `Task` | Subscribe to data stream. Optional `startTime` enables intraday replay. |
| `SubscribeWithSnapshotAsync` | `dataset`: string<br>`schema`: Schema<br>`symbols`: IEnumerable&lt;string&gt;<br>`cancellationToken`: CancellationToken = default | `Task` | Subscribe with initial MBO snapshot |

#### 2.2.4 Session Control Methods

| Method | Input Parameters | Output | Description |
|--------|------------------|--------|-------------|
| `StartAsync` | `cancellationToken`: CancellationToken = default | `Task<DbnMetadata>` | Start receiving data. Returns session metadata. |
| `StopAsync` | `cancellationToken`: CancellationToken = default | `Task` | Stop receiving data |
| `ReconnectAsync` | `cancellationToken`: CancellationToken = default | `Task` | Reconnect after disconnection |
| `ResubscribeAsync` | `cancellationToken`: CancellationToken = default | `Task` | Resubscribe to all previous subscriptions |

#### 2.2.5 Streaming Methods

| Method | Input Parameters | Output | Description |
|--------|------------------|--------|-------------|
| `StreamAsync` | `cancellationToken`: CancellationToken = default | `IAsyncEnumerable<Record>` | Stream records as async enumerable |
| `BlockUntilStoppedAsync` | `cancellationToken`: CancellationToken = default | `Task` | Block until stream stops |
| `BlockUntilStoppedAsync` (timeout) | `timeout`: TimeSpan<br>`cancellationToken`: CancellationToken = default | `Task<bool>` | Block with timeout. Returns `true` if stopped, `false` if timeout. |

---

### 2.3 LiveBlockingClient

**Interface:** `ILiveBlockingClient`
**Implementation:** `LiveBlockingClient`
**Purpose:** Pull-based blocking client for real-time market data (synchronous control)
**Lifecycle:** `IAsyncDisposable`

#### 2.3.1 Construction via Builder

```csharp
var client = new LiveBlockingClientBuilder()
    .WithApiKey(apiKey)                    // Required - Databento API key
    .WithDataset(dataset)                  // Required - Dataset for connection (native library requirement)
    .WithSendTsOut(sendTsOut)              // Optional - Include ts_out timestamps (default: false)
    .WithUpgradePolicy(policy)             // Optional - AsIs or Upgrade (default)
    .WithHeartbeatInterval(interval)       // Optional - Connection heartbeat (default: 30s)
    .WithLogger(logger)                    // Optional - ILogger<ILiveBlockingClient>
    .Build();
```

#### 2.3.2 Subscription Methods

| Method | Input Parameters | Output | Description |
|--------|------------------|--------|-------------|
| `SubscribeAsync` | `dataset`: string<br>`schema`: Schema<br>`symbols`: IEnumerable&lt;string&gt;<br>`cancellationToken`: CancellationToken = default | `Task` | Subscribe to data stream |
| `SubscribeWithReplayAsync` | `dataset`: string<br>`schema`: Schema<br>`symbols`: IEnumerable&lt;string&gt;<br>`start`: DateTimeOffset<br>`cancellationToken`: CancellationToken = default | `Task` | Subscribe with historical replay from start time |
| `SubscribeWithSnapshotAsync` | `dataset`: string<br>`schema`: Schema<br>`symbols`: IEnumerable&lt;string&gt;<br>`cancellationToken`: CancellationToken = default | `Task` | Subscribe with MBO snapshot |

#### 2.3.3 Session Control Methods

| Method | Input Parameters | Output | Description |
|--------|------------------|--------|-------------|
| `StartAsync` | `cancellationToken`: CancellationToken = default | `Task<DbnMetadata>` | Start stream. Blocks until metadata received. |
| `NextRecordAsync` | `timeout`: TimeSpan? = null<br>`cancellationToken`: CancellationToken = default | `Task<Record?>` | Pull next record. Returns `null` on timeout. Use `Timeout.InfiniteTimeSpan` for no timeout. |
| `ReconnectAsync` | `cancellationToken`: CancellationToken = default | `Task` | Reconnect to gateway |
| `ResubscribeAsync` | `cancellationToken`: CancellationToken = default | `Task` | Resubscribe to stored subscriptions |
| `StopAsync` | `cancellationToken`: CancellationToken = default | `Task` | Stop the stream |

---

## 3. Data Models

### 3.1 Record Types

All record types inherit from the base `Record` class:

```csharp
public abstract class Record
{
    public long TimestampNs { get; set; }        // Timestamp in nanoseconds since Unix epoch
    public byte RType { get; set; }              // Record type identifier
    public ushort PublisherId { get; set; }      // Publisher ID
    public uint InstrumentId { get; set; }       // Instrument ID
    public DateTimeOffset Timestamp { get; }     // Convenience property (derived from TimestampNs)
    internal byte[]? RawBytes { get; set; }      // Raw DBN bytes for serialization
}
```

#### TradeMessage

**Size:** 48 bytes | **RType:** 0x00 | **Schema:** `trades`

| Property | Type | Description |
|----------|------|-------------|
| `Price` | `long` | Trade price (fixed-point, 9 decimal places) |
| `Size` | `uint` | Trade size (volume) |
| `Action` | `Action` | Trade action (Trade, Fill, etc.) |
| `Side` | `Side` | Trade side (Bid, Ask, None) |
| `Flags` | `byte` | Trade flags |
| `Depth` | `byte` | Trade depth |
| `Sequence` | `uint` | Sequence number |
| `PriceDecimal` | `decimal` | Convenience: Price / 1,000,000,000 |

#### MboMessage

**Size:** 56 bytes | **RType:** 0xA0 | **Schema:** `mbo`

| Property | Type | Description |
|----------|------|-------------|
| `OrderId` | `ulong` | Order ID |
| `Price` | `long` | Order price (fixed-point) |
| `Size` | `uint` | Order size |
| `Flags` | `byte` | Order flags |
| `ChannelId` | `byte` | Channel ID |
| `Action` | `Action` | Order action (Add, Modify, Cancel, Trade, Fill, Clear) |
| `Side` | `Side` | Order side |
| `TsRecv` | `long` | Receive timestamp (ns) |
| `TsInDelta` | `int` | Timestamp delta |
| `Sequence` | `uint` | Sequence number |

#### Mbp1Message

**Size:** 80 bytes | **RType:** 0x01 | **Schema:** `mbp-1`

| Property | Type | Description |
|----------|------|-------------|
| `Price` | `long` | Price (fixed-point) |
| `Size` | `uint` | Size |
| `Action` | `Action` | Action type |
| `Side` | `Side` | Side |
| `Flags` | `byte` | Flags |
| `Depth` | `byte` | Depth |
| `TsRecv` | `long` | Receive timestamp (ns) |
| `TsInDelta` | `int` | Timestamp delta |
| `Sequence` | `uint` | Sequence number |
| `Level` | `BidAskPair` | Best bid/ask level |

#### Mbp10Message

**Size:** 368 bytes | **RType:** 0x0A | **Schema:** `mbp-10`

Same as Mbp1Message plus:

| Property | Type | Description |
|----------|------|-------------|
| `Levels` | `BidAskPair[10]` | Array of 10 price levels |

#### OhlcvMessage

**Size:** 56 bytes | **RTypes:** 0x20-0x24 | **Schemas:** `ohlcv-1s`, `ohlcv-1m`, `ohlcv-1h`, `ohlcv-1d`, `ohlcv-eod`

| Property | Type | Description |
|----------|------|-------------|
| `Open` | `long` | Open price (fixed-point) |
| `High` | `long` | High price (fixed-point) |
| `Low` | `long` | Low price (fixed-point) |
| `Close` | `long` | Close price (fixed-point) |
| `Volume` | `ulong` | Volume |

RType mapping:
- 0x20: OHLCV 1-second
- 0x21: OHLCV 1-minute
- 0x22: OHLCV 1-hour
- 0x23: OHLCV 1-day
- 0x24: OHLCV end-of-day

#### StatusMessage

**Size:** 40 bytes | **RType:** 0x12 | **Schema:** `status`

| Property | Type | Description |
|----------|------|-------------|
| `TsRecv` | `long` | Receive timestamp (ns) |
| `Action` | `StatusAction` | Status action (PreOpen, Trading, Halt, Close, etc.) |
| `Reason` | `StatusReason` | Status reason (Scheduled, SurveillanceIntervention, etc.) |
| `TradingEvent` | `TradingEvent` | Trading event type |
| `IsTrading` | `TriState` | Trading status (Yes, No, NotAvailable) |
| `IsQuoting` | `TriState` | Quoting status |
| `IsShortSellRestricted` | `TriState` | Short sell restriction |

#### InstrumentDefMessage

**Size:** 520 bytes | **RType:** 0x13 | **Schema:** `definition`

| Property | Type | Description |
|----------|------|-------------|
| `TsRecv` | `long` | Receive timestamp (ns) |
| `MinPriceIncrement` | `long` | Tick size (fixed-point) |
| `DisplayFactor` | `long` | Display factor |
| `Expiration` | `long` | Expiration timestamp (ns) |
| `Activation` | `long` | Activation timestamp (ns) |
| `HighLimitPrice` | `long` | High limit price (fixed-point) |
| `LowLimitPrice` | `long` | Low limit price (fixed-point) |
| `MaxPriceVariation` | `long` | Max price variation (fixed-point) |
| `StrikePrice` | `long` | Strike price for options (fixed-point) |
| `RawInstrumentId` | `ulong` | Raw 64-bit instrument ID |
| `UnderlyingId` | `uint` | Underlying instrument ID |
| `MarketDepth` | `int` | Market depth |
| `ContractMultiplier` | `int` | Contract multiplier |
| `Currency` | `string` | Currency code (e.g., "USD") |
| `SettlCurrency` | `string` | Settlement currency |
| `RawSymbol` | `string` | Raw symbol string (71 chars max) |
| `Group` | `string` | Security group (21 chars max) |
| `Exchange` | `string` | Exchange code (5 chars max) |
| `Asset` | `string` | Asset class (11 chars max) |
| `Cfi` | `string` | CFI code (7 chars max) |
| `SecurityType` | `string` | Security type (7 chars max) |
| `UnitOfMeasure` | `string` | Unit of measure (31 chars max) |
| `Underlying` | `string` | Underlying symbol (21 chars max) |
| `InstrumentClass` | `InstrumentClass` | Instrument class (Future, Call, Put, Stock, etc.) |
| `MatchAlgorithm` | `MatchAlgorithm` | Match algorithm (FIFO, ProRata, etc.) |
| `MaturityYear` | `ushort` | Maturity year |
| `MaturityMonth` | `byte` | Maturity month (1-12) |
| `MaturityDay` | `byte` | Maturity day (1-31) |
| `LegCount` | `ushort` | Number of legs (multi-leg instruments) |
| `LegIndex` | `ushort` | Leg index (0-based) |
| `LegInstrumentId` | `uint` | Leg instrument ID |
| `LegRawSymbol` | `string` | Leg raw symbol (71 chars max) |
| `LegSide` | `Side` | Leg side |

Convenience properties: `StrikePriceDecimal`, `HighLimitPriceDecimal`, `LowLimitPriceDecimal`, `MinPriceIncrementDecimal`, `ExpirationTime`, `TsRecvTime`

#### ImbalanceMessage

**Size:** 112 bytes | **RType:** 0x14 | **Schema:** `imbalance`

| Property | Type | Description |
|----------|------|-------------|
| `TsRecv` | `long` | Receive timestamp (ns) |
| `RefPrice` | `long` | Reference price (fixed-point) |
| `AuctionTime` | `long` | Auction time (ns) |
| `PairedQty` | `ulong` | Paired quantity |
| `TotalImbalanceQty` | `ulong` | Total imbalance quantity |
| `Side` | `Side` | Imbalance side |

#### StatMessage

**Size:** 80 bytes | **RType:** 0x18 | **Schema:** `statistics`

| Property | Type | Description |
|----------|------|-------------|
| `TsRecv` | `long` | Receive timestamp (ns) |
| `TsRef` | `long` | Reference timestamp (ns) |
| `Price` | `long` | Price (fixed-point) |
| `Quantity` | `long` | Quantity |
| `Sequence` | `uint` | Sequence number |
| `TsInDelta` | `int` | Timestamp delta |
| `StatType` | `ushort` | Statistic type |
| `ChannelId` | `ushort` | Channel ID |
| `UpdateAction` | `byte` | Update action |
| `StatFlags` | `byte` | Statistic flags |

#### ErrorMessage

**Size:** 320 bytes | **RType:** 0x15

| Property | Type | Description |
|----------|------|-------------|
| `Error` | `string` | Error message (302 chars max) |
| `Code` | `ErrorCode` | Error code |
| `IsLast` | `bool` | Is last error in sequence |

#### SystemMessage

**Size:** 320 bytes | **RType:** 0x17

| Property | Type | Description |
|----------|------|-------------|
| `Message` | `string` | System message (303 chars max) |
| `Code` | `SystemCode` | System code (Heartbeat, SubscriptionAck, SlowReaderWarning, etc.) |

#### SymbolMappingMessage

**Size:** 176 bytes | **RType:** 0x16

| Property | Type | Description |
|----------|------|-------------|
| `STypeIn` | `SType` | Input symbology type |
| `STypeInSymbol` | `string` | Input symbol (71 chars max) |
| `STypeOut` | `SType` | Output symbology type |
| `STypeOutSymbol` | `string` | Output symbol (71 chars max) |
| `StartTs` | `long` | Start timestamp (ns) |
| `EndTs` | `long` | End timestamp (ns) |

#### BboMessage

**Size:** 80 bytes | **RTypes:** 0xC3 (bbo-1s), 0xC4 (bbo-1m) | **Schemas:** `bbo-1s`, `bbo-1m`

| Property | Type | Description |
|----------|------|-------------|
| `Price` | `long` | Price (fixed-point) |
| `Size` | `uint` | Size |
| `Side` | `Side` | Side |
| `Flags` | `byte` | Flags |
| `TsRecv` | `long` | Receive timestamp (ns) |
| `Sequence` | `uint` | Sequence number |
| `Level` | `BidAskPair` | Best bid/ask |

#### CbboMessage

**Size:** 80 bytes | **RTypes:** 0xC0 (cbbo-1s), 0xC1 (cbbo-1m) | **Schemas:** `cbbo-1s`, `cbbo-1m`

| Property | Type | Description |
|----------|------|-------------|
| `Price` | `long` | Price (fixed-point) |
| `Size` | `uint` | Size |
| `Side` | `Side` | Side |
| `Flags` | `byte` | Flags |
| `TsRecv` | `long` | Receive timestamp (ns) |
| `Sequence` | `uint` | Sequence number |
| `Level` | `ConsolidatedBidAskPair` | Consolidated best bid/ask |

#### Cmbp1Message

**Size:** 80 bytes | **RType:** 0xB1 | **Schema:** `cmbp-1`

| Property | Type | Description |
|----------|------|-------------|
| `Price` | `long` | Price (fixed-point) |
| `Size` | `uint` | Size |
| `Action` | `Action` | Action type |
| `Side` | `Side` | Side |
| `Flags` | `byte` | Flags |
| `TsRecv` | `long` | Receive timestamp (ns) |
| `TsInDelta` | `int` | Timestamp delta |
| `Level` | `ConsolidatedBidAskPair` | Consolidated level |

#### TcbboMessage

**Size:** 80 bytes | **RType:** 0xC2 | **Schema:** `tcbbo`

Trade with Consolidated BBO - same structure as Cmbp1Message.

---

### 3.2 Supporting Structures

#### BidAskPair (32 bytes)

| Property | Type | Description |
|----------|------|-------------|
| `BidPrice` | `long` | Bid price (fixed-point) |
| `AskPrice` | `long` | Ask price (fixed-point) |
| `BidSize` | `uint` | Bid size |
| `AskSize` | `uint` | Ask size |
| `BidCount` | `uint` | Bid order count |
| `AskCount` | `uint` | Ask order count |

#### ConsolidatedBidAskPair (32 bytes)

| Property | Type | Description |
|----------|------|-------------|
| `BidPrice` | `long` | Bid price (fixed-point) |
| `AskPrice` | `long` | Ask price (fixed-point) |
| `BidSize` | `uint` | Bid size |
| `AskSize` | `uint` | Ask size |
| `BidPublisher` | `ushort` | Bid publisher ID |
| `AskPublisher` | `ushort` | Ask publisher ID |

#### DbnMetadata

| Property | Type | Description |
|----------|------|-------------|
| `Version` | `byte` | DBN version |
| `Dataset` | `string` | Dataset name |
| `Schema` | `Schema` | Schema type |
| `Start` | `DateTimeOffset` | Start time |
| `End` | `DateTimeOffset` | End time |
| `Limit` | `ulong?` | Record limit |
| `StypeIn` | `SType` | Input symbology type |
| `StypeOut` | `SType` | Output symbology type |
| `Symbols` | `string[]` | Symbols array |
| `MappingsCount` | `int` | Number of symbol mappings |

---

## 4. Enumerations

### Schema

| Value | Code | API String | Description |
|-------|------|------------|-------------|
| `Mbo` | 0 | `mbo` | Market by order (full order book) |
| `Mbp1` | 1 | `mbp-1` | Market by price level 1 (top of book) |
| `Mbp10` | 2 | `mbp-10` | Market by price level 10 (10 levels) |
| `Tbbo` | 3 | `tbbo` | Trades with BBO |
| `Trades` | 4 | `trades` | All trades |
| `Ohlcv1S` | 5 | `ohlcv-1s` | OHLCV 1 second bars |
| `Ohlcv1M` | 6 | `ohlcv-1m` | OHLCV 1 minute bars |
| `Ohlcv1H` | 7 | `ohlcv-1h` | OHLCV 1 hour bars |
| `Ohlcv1D` | 8 | `ohlcv-1d` | OHLCV 1 day bars (UTC) |
| `Definition` | 9 | `definition` | Instrument definitions |
| `Statistics` | 10 | `statistics` | Market statistics |
| `Status` | 11 | `status` | Trading status events |
| `Imbalance` | 12 | `imbalance` | Auction imbalances |
| `OhlcvEod` | 13 | `ohlcv-eod` | OHLCV end of day (session-based) |
| `Cmbp1` | 14 | `cmbp-1` | Consolidated MBP-1 |
| `Cbbo1S` | 15 | `cbbo-1s` | Consolidated BBO 1 second |
| `Cbbo1M` | 16 | `cbbo-1m` | Consolidated BBO 1 minute |
| `Tcbbo` | 17 | `tcbbo` | Trades with consolidated BBO |
| `Bbo1S` | 18 | `bbo-1s` | BBO 1 second |
| `Bbo1M` | 19 | `bbo-1m` | BBO 1 minute |

### SType (Symbology Type)

| Value | Code | API String | Description |
|-------|------|------------|-------------|
| `InstrumentId` | 0 | `instrument_id` | Numeric instrument ID |
| `RawSymbol` | 1 | `raw_symbol` | Raw exchange symbol |
| `Smart` | 2 | `smart` | Smart routing |
| `Continuous` | 3 | `continuous` | Continuous contracts (e.g., ES.c.0) |
| `Parent` | 4 | `parent` | Parent symbol |
| `NasdaqSymbol` | 5 | `nasdaq_symbol` | NASDAQ symbol |
| `CmsSymbol` | 6 | `cms_symbol` | CMS symbol |
| `Isin` | 7 | `isin` | ISIN identifier |
| `UsCode` | 8 | `us_code` | US code |
| `BbgCompId` | 9 | `bbg_comp_id` | Bloomberg company ID |
| `BbgCompTicker` | 10 | `bbg_comp_ticker` | Bloomberg ticker |
| `Figi` | 11 | `figi` | FIGI identifier |
| `FigiTicker` | 12 | `figi_ticker` | FIGI ticker |

### Side

| Value | Code | Description |
|-------|------|-------------|
| `Ask` | 'A' (65) | Ask/offer side |
| `Bid` | 'B' (66) | Bid side |
| `None` | 'N' (78) | No side |

### Action

| Value | Code | Description |
|-------|------|-------------|
| `Modify` | 'M' (77) | Order modification |
| `Trade` | 'T' (84) | Trade execution |
| `Fill` | 'F' (70) | Order fill |
| `Cancel` | 'C' (67) | Order cancellation |
| `Add` | 'A' (65) | Order addition |
| `Clear` | 'R' (82) | Clear/reset |
| `None` | 'N' (78) | No action |

### InstrumentClass

| Value | Code | Description |
|-------|------|-------------|
| `Unknown` | 0 | Unknown |
| `Bond` | 'B' (66) | Bond |
| `Call` | 'C' (67) | Call option |
| `Future` | 'F' (70) | Future |
| `Stock` | 'K' (75) | Stock |
| `MixedSpread` | 'M' (77) | Mixed spread |
| `Put` | 'P' (80) | Put option |
| `FutureSpread` | 'S' (83) | Future spread |
| `OptionSpread` | 'T' (84) | Option spread |
| `FxSpot` | 'X' (88) | FX spot |
| `CommoditySpot` | 'Y' (89) | Commodity spot |

### MatchAlgorithm

| Value | Code | Description |
|-------|------|-------------|
| `Undefined` | '0' (48) | Undefined |
| `Fifo` | 'F' (70) | First in, first out |
| `Configurable` | 'K' (75) | Configurable |
| `ProRata` | 'C' (67) | Pro-rata allocation |
| `FifoLmm` | 'T' (84) | FIFO with lead market maker |
| `ThresholdProRata` | 'O' (79) | Threshold pro-rata |
| `FifoTopLmm` | 'S' (83) | FIFO top with LMM |
| `ThresholdProRataLmm` | 'Q' (81) | Threshold pro-rata with LMM |
| `Eurodollar` | 'Y' (89) | Eurodollar |

### StatusAction

| Value | Code | Description |
|-------|------|-------------|
| `None` | 0 | No action |
| `PreOpen` | 1 | Pre-open |
| `PreCross` | 2 | Pre-cross |
| `Quoting` | 3 | Quoting |
| `Cross` | 4 | Cross |
| `Rotation` | 5 | Rotation |
| `NewPriceIndication` | 6 | New price indication |
| `Trading` | 7 | Trading |
| `Halt` | 8 | Halt |
| `Pause` | 9 | Pause |
| `Suspend` | 10 | Suspend |
| `PreClose` | 11 | Pre-close |
| `Close` | 12 | Close |
| `PostClose` | 13 | Post-close |
| `Closed` | 14 | Closed |
| `PrivateAuction` | 200 | Private auction |

### StatusReason

| Value | Code | Description |
|-------|------|-------------|
| `None` | 0 | No reason |
| `Scheduled` | 1 | Scheduled |
| `SurveillanceIntervention` | 2 | Surveillance intervention |
| `MarketEvent` | 3 | Market event |
| `InstrumentActivation` | 4 | Instrument activation |
| `InstrumentExpiration` | 5 | Instrument expiration |
| `Recovery` | 6 | Recovery |
| `Compliance` | 7 | Compliance |
| `Regulatory` | 8 | Regulatory |
| `AdministrativeEnd` | 9 | Administrative end |
| `AdministrativeSuspend` | 10 | Administrative suspend |
| `NotAvailable` | 11 | Not available |

### ErrorCode

| Value | Code | Description |
|-------|------|-------------|
| `AuthFailed` | 1 | Authentication failed |
| `ApiKeyDeactivated` | 2 | API key deactivated |
| `ConnectionLimitExceeded` | 3 | Connection limit exceeded |
| `SymbolResolutionFailed` | 4 | Symbol resolution failed |
| `InvalidSubscription` | 5 | Invalid subscription |
| `InternalError` | 6 | Internal gateway error |
| `Unset` | 255 | No error code specified |

### SystemCode

| Value | Code | Description |
|-------|------|-------------|
| `Heartbeat` | 0 | Connection heartbeat |
| `SubscriptionAck` | 1 | Subscription acknowledged |
| `SlowReaderWarning` | 2 | Slow reader warning |
| `ReplayCompleted` | 3 | Replay caught up with real-time |
| `EndOfInterval` | 4 | End of interval signal |
| `Unset` | 255 | No system code specified |

### Configuration Enums

#### HistoricalGateway

| Value | Description |
|-------|-------------|
| `Bo1` | Primary gateway (bo1.databento.com) |
| `Bo2` | Secondary gateway (bo2.databento.com) |
| `Custom` | Custom gateway address |

#### VersionUpgradePolicy

| Value | Description |
|-------|-------------|
| `AsIs` | Keep original DBN version |
| `Upgrade` | Upgrade to latest DBN version |

#### Encoding

| Value | API String | Description |
|-------|------------|-------------|
| `Dbn` | `dbn` | Databento Binary Encoding |
| `Csv` | `csv` | Comma-separated values |
| `Json` | `json` | JSON format |

#### Compression

| Value | API String | Description |
|-------|------------|-------------|
| `None` | `none` | No compression |
| `Zstd` | `zstd` | Zstandard compression |
| `Gzip` | `gzip` | Gzip compression |

#### SplitDuration

| Value | API String | Description |
|-------|------------|-------------|
| `None` | `none` | No splitting |
| `Day` | `day` | Split by day |
| `Week` | `week` | Split by week |
| `Month` | `month` | Split by month |

#### Delivery

| Value | Description |
|-------|-------------|
| `Download` | Direct download |
| `S3` | AWS S3 delivery |
| `Disk` | Local disk |

#### JobState

| Value | API String | Description |
|-------|------------|-------------|
| `Received` | `received` | Job received |
| `Queued` | `queued` | Job queued |
| `Processing` | `processing` | Job processing |
| `Done` | `done` | Job completed |
| `Expired` | `expired` | Job expired |

#### DatasetCondition

| Value | API String | Description |
|-------|------------|-------------|
| `Available` | `available` | Dataset available |
| `Degraded` | `degraded` | Degraded availability |
| `Pending` | `pending` | Availability pending |
| `Missing` | `missing` | Dataset missing |

### RType (Record Type Identifier)

Critical enum for DBN binary protocol message routing. Values match databento-cpp `enums.hpp`.

| Value | Code (Hex) | Description |
|-------|------------|-------------|
| `Mbp0` | 0x00 | Trade messages |
| `Mbp1` | 0x01 | Market by Price Level 1 |
| `Mbp10` | 0x0A | Market by Price Level 10 |
| `OhlcvDeprecated` | 0x11 | Deprecated OHLCV format |
| `Status` | 0x12 | Trading status messages |
| `InstrumentDef` | 0x13 | Instrument definitions |
| `Imbalance` | 0x14 | Order imbalances |
| `Error` | 0x15 | Error messages |
| `SymbolMapping` | 0x16 | Symbol mapping messages |
| `System` | 0x17 | System messages / heartbeats |
| `Statistics` | 0x18 | Market statistics |
| `Ohlcv1S` | 0x20 | OHLCV 1 second bars |
| `Ohlcv1M` | 0x21 | OHLCV 1 minute bars |
| `Ohlcv1H` | 0x22 | OHLCV 1 hour bars |
| `Ohlcv1D` | 0x23 | OHLCV 1 day bars |
| `OhlcvEod` | 0x24 | OHLCV end of day bars |
| `Mbo` | 0xA0 | Market by Order (full order book) |
| `Cmbp1` | 0xB1 | Consolidated Market by Price Level 1 |
| `Cbbo1S` | 0xC0 | Consolidated BBO 1 second |
| `Cbbo1M` | 0xC1 | Consolidated BBO 1 minute |
| `Tcbbo` | 0xC2 | Trade with Consolidated BBO |
| `Bbo1S` | 0xC3 | BBO 1 second |
| `Bbo1M` | 0xC4 | BBO 1 minute |

### TradingEvent

| Value | Code | Description |
|-------|------|-------------|
| `None` | 0 | No trading event |
| `NoCancel` | 1 | No cancel period |
| `ChangeTradingSession` | 2 | Trading session change |
| `ImpliedMatchingOn` | 3 | Implied matching enabled |
| `ImpliedMatchingOff` | 4 | Implied matching disabled |

### TriState

| Value | Code | Description |
|-------|------|-------------|
| `NotAvailable` | '~' (126) | Value not available |
| `No` | 'N' (78) | No / False |
| `Yes` | 'Y' (89) | Yes / True |

### UserDefinedInstrument

| Value | Code | Description |
|-------|------|-------------|
| `No` | 'N' (78) | Not user-defined |
| `Yes` | 'Y' (89) | User-defined instrument |

### SecurityUpdateAction

| Value | Code | Description |
|-------|------|-------------|
| `Add` | 'A' (65) | Security added |
| `Modify` | 'M' (77) | Security modified |
| `Delete` | 'D' (68) | Security deleted |

### PricingMode

| Value | Code | Description |
|-------|------|-------------|
| `Historical` | 0 | Historical batch data pricing |
| `HistoricalStreaming` | 1 | Historical streaming data pricing |
| `Live` | 2 | Live streaming data pricing |

### ConnectionState

| Value | Description |
|-------|-------------|
| `Disconnected` | Not connected to gateway |
| `Connecting` | Connecting to gateway |
| `Connected` | Connected and authenticated |
| `Streaming` | Actively streaming data |
| `Reconnecting` | Reconnecting after disconnection |
| `Stopped` | Stopped by user |

### ExceptionAction

| Value | Description |
|-------|-------------|
| `Continue` | Continue processing after the exception |
| `Stop` | Stop streaming and clean up |

---

## 5. Builder Classes

### HistoricalClientBuilder

| Method | Parameter | Type | Required | Default | Description |
|--------|-----------|------|----------|---------|-------------|
| `WithApiKey` | `apiKey` | `string` | **Yes** | - | Databento API key |
| `WithGateway` | `gateway` | `HistoricalGateway` | No | `Bo1` | API gateway selection |
| `WithAddress` | `host`, `port` | `string`, `ushort` | No | - | Custom gateway address (sets gateway to Custom) |
| `WithUpgradePolicy` | `policy` | `VersionUpgradePolicy` | No | `Upgrade` | DBN version upgrade policy |
| `WithUserAgent` | `userAgent` | `string` | No | - | Additional user agent string |
| `WithTimeout` | `timeout` | `TimeSpan` | No | 30 seconds | Request timeout (must be positive) |
| `WithLogger` | `logger` | `ILogger<IHistoricalClient>` | No | - | Logger for diagnostics |
| `Build` | - | - | - | - | Returns `IHistoricalClient` |

### LiveClientBuilder

| Method | Parameter | Type | Required | Default | Description |
|--------|-----------|------|----------|---------|-------------|
| `WithApiKey` | `apiKey` | `string` | **Yes** | - | Databento API key |
| `WithDataset` | `dataset` | `string` | No | - | Default dataset for subscriptions |
| `WithSendTsOut` | `sendTsOut` | `bool` | No | `false` | Include ts_out timestamps in records |
| `WithUpgradePolicy` | `policy` | `VersionUpgradePolicy` | No | `Upgrade` | DBN version upgrade policy |
| `WithHeartbeatInterval` | `interval` | `TimeSpan` | No | 30 seconds | Heartbeat interval (must be positive) |
| `WithLogger` | `logger` | `ILogger<ILiveClient>` | No | - | Logger for diagnostics |
| `WithExceptionHandler` | `handler` | `ExceptionCallback` | No | - | Exception handler for streaming errors |
| `Build` | - | - | - | - | Returns `ILiveClient` |

### LiveBlockingClientBuilder

| Method | Parameter | Type | Required | Default | Description |
|--------|-----------|------|----------|---------|-------------|
| `WithApiKey` | `apiKey` | `string` | **Yes** | - | Databento API key |
| `WithDataset` | `dataset` | `string` | **Yes** | - | Dataset for connection (native library requirement) |
| `WithSendTsOut` | `sendTsOut` | `bool` | No | `false` | Include ts_out timestamps in records |
| `WithUpgradePolicy` | `policy` | `VersionUpgradePolicy` | No | `Upgrade` | DBN version upgrade policy |
| `WithHeartbeatInterval` | `interval` | `TimeSpan` | No | 30 seconds | Heartbeat interval (must be positive) |
| `WithLogger` | `logger` | `ILogger<ILiveBlockingClient>` | No | - | Logger for diagnostics |
| `Build` | - | - | - | - | Returns `ILiveBlockingClient` |

### ReferenceClientBuilder

| Method | Parameter | Type | Required | Default | Description |
|--------|-----------|------|----------|---------|-------------|
| `WithApiKey` | `apiKey` | `string` | No* | - | Databento API key (*falls back to `DATABENTO_API_KEY` env var) |
| `WithGateway` | `gateway` | `HistoricalGateway` | No | `Bo1` | API gateway selection |
| `WithLogger` | `logger` | `ILogger<IReferenceClient>` | No | - | Logger for diagnostics |
| `WithHttpClient` | `httpClient` | `HttpClient` | No | - | Pre-configured HttpClient (e.g., from IHttpClientFactory) |
| `Build` | - | - | - | - | Returns `IReferenceClient` |

---

## 5.1 ReferenceClient

**Interface:** `IReferenceClient`
**Implementation:** `ReferenceClient`
**Purpose:** Query reference data including corporate actions, adjustment factors, and security master data
**Lifecycle:** `IAsyncDisposable`

### 5.1.1 Construction via Builder

```csharp
var client = new ReferenceClientBuilder()
    .WithApiKey(apiKey)           // Optional - falls back to DATABENTO_API_KEY env var
    .WithGateway(gateway)         // Optional - Bo1 (default), Bo2, Custom
    .WithLogger(logger)           // Optional - ILogger<IReferenceClient>
    .WithHttpClient(httpClient)   // Optional - Pre-configured HttpClient
    .Build();
```

### 5.1.2 Sub-APIs

| Property | Type | Description |
|----------|------|-------------|
| `CorporateActions` | `ICorporateActionsApi` | Corporate actions reference data |
| `AdjustmentFactors` | `IAdjustmentFactorsApi` | Adjustment factors reference data |
| `SecurityMaster` | `ISecurityMasterApi` | Security master reference data |

### 5.1.3 ICorporateActionsApi

| Method | Input Parameters | Output | Description |
|--------|------------------|--------|-------------|
| `GetRangeAsync` | `start`: DateTimeOffset<br>`end`: DateTimeOffset? = null<br>`index`: string = "event_date"<br>`symbols`: IEnumerable&lt;string&gt;? = null<br>`stypeIn`: SType = RawSymbol<br>`events`: IEnumerable&lt;string&gt;? = null<br>`countries`: IEnumerable&lt;string&gt;? = null<br>`exchanges`: IEnumerable&lt;string&gt;? = null<br>`securityTypes`: IEnumerable&lt;string&gt;? = null<br>`flatten`: bool = true<br>`pit`: bool = false<br>`cancellationToken`: CancellationToken = default | `Task<List<CorporateActionRecord>>` | Get corporate actions time series data |

### 5.1.4 IAdjustmentFactorsApi

| Method | Input Parameters | Output | Description |
|--------|------------------|--------|-------------|
| `GetRangeAsync` | `start`: DateTimeOffset<br>`end`: DateTimeOffset? = null<br>`symbols`: IEnumerable&lt;string&gt;? = null<br>`stypeIn`: SType = RawSymbol<br>`countries`: IEnumerable&lt;string&gt;? = null<br>`securityTypes`: IEnumerable&lt;string&gt;? = null<br>`cancellationToken`: CancellationToken = default | `Task<List<AdjustmentFactorRecord>>` | Get adjustment factors time series data |

### 5.1.5 ISecurityMasterApi

| Method | Input Parameters | Output | Description |
|--------|------------------|--------|-------------|
| `GetLastAsync` | `symbols`: IEnumerable&lt;string&gt;? = null<br>`stypeIn`: SType = RawSymbol<br>`countries`: IEnumerable&lt;string&gt;? = null<br>`securityTypes`: IEnumerable&lt;string&gt;? = null<br>`cancellationToken`: CancellationToken = default | `Task<List<SecurityMasterRecord>>` | Get latest security master data |
| `GetRangeAsync` | `start`: DateTimeOffset<br>`end`: DateTimeOffset? = null<br>`index`: string = "ts_effective"<br>`symbols`: IEnumerable&lt;string&gt;? = null<br>`stypeIn`: SType = RawSymbol<br>`countries`: IEnumerable&lt;string&gt;? = null<br>`securityTypes`: IEnumerable&lt;string&gt;? = null<br>`cancellationToken`: CancellationToken = default | `Task<List<SecurityMasterRecord>>` | Get security master PIT time series data |

---

## 6. Symbol Mapping APIs

### IMetadata

| Method | Input | Output | Description |
|--------|-------|--------|-------------|
| `GetSymbol` | `instrumentId`: uint | `string?` | Get symbol for instrument ID (null if not found) |
| `Contains` | `instrumentId`: uint | `bool` | Check if mapping exists |
| `CreateSymbolMap` | - | `ITsSymbolMap` | Create timeseries symbol map (for multi-day data) |
| `CreateSymbolMapForDate` | `date`: DateOnly | `IPitSymbolMap` | Create point-in-time symbol map (for single day/live) |

### ITsSymbolMap (Timeseries Symbol Map)

For historical data spanning multiple days where symbols may change.

| Property/Method | Input | Output | Description |
|-----------------|-------|--------|-------------|
| `IsEmpty` | - | `bool` | Whether map is empty |
| `Size` | - | `int` | Number of mappings |
| `Find` | `date`: DateOnly, `instrumentId`: uint | `string?` | Find symbol for date and ID |
| `At` | `date`: DateOnly, `instrumentId`: uint | `string` | Get symbol (throws `KeyNotFoundException` if not found) |
| `Find` | `record`: Record | `string?` | Find symbol for record (extracts date and ID) |
| `At` | `record`: Record | `string` | Get symbol for record (throws if not found) |

### IPitSymbolMap (Point-in-Time Symbol Map)

For live data or single-day historical data.

| Property/Method | Input | Output | Description |
|-----------------|-------|--------|-------------|
| `IsEmpty` | - | `bool` | Whether map is empty |
| `Size` | - | `int` | Number of mappings |
| `Find` | `instrumentId`: uint | `string?` | Find symbol for ID |
| `At` | `instrumentId`: uint | `string` | Get symbol (throws `KeyNotFoundException` if not found) |
| `Find` | `record`: Record | `string?` | Find symbol for record |
| `At` | `record`: Record | `string` | Get symbol for record (throws if not found) |
| `OnRecord` | `record`: Record | `void` | Update map from record (processes SymbolMappingMessage) |
| `OnSymbolMapping` | `symbolMapping`: SymbolMappingMessage | `void` | Update map from symbol mapping message directly |

---

## 7. DBN File I/O

### IDbnFileReader

| Method | Input | Output | Description |
|--------|-------|--------|-------------|
| `GetMetadata` | - | `DbnMetadata` | Get file metadata |
| `ReadRecordsAsync` | `cancellationToken`: CancellationToken = default | `IAsyncEnumerable<Record>` | Read all records as async stream |

**Lifecycle:** Implements both `IDisposable` and `IAsyncDisposable`

### IDbnFileWriter

| Method | Input | Output | Description |
|--------|-------|--------|-------------|
| `WriteRecord` | `record`: Record | `void` | Write single record |
| `WriteRecords` | `records`: IEnumerable&lt;Record&gt; | `void` | Write multiple records |
| `Flush` | - | `void` | Flush buffered data to disk |

**Lifecycle:** Implements both `IDisposable` and `IAsyncDisposable`

---

## 8. Native C API Reference

The native layer (`databento_native.dll`) provides a C ABI for P/Invoke interoperability.

### 8.1 Opaque Handle Types

| Handle Type | Description |
|-------------|-------------|
| `DbentoLiveClientHandle` | Live client handle (threaded mode) |
| `DbentoHistoricalClientHandle` | Historical client handle |
| `DbentoMetadataHandle` | Query metadata handle |
| `DbentoTsSymbolMapHandle` | Timeseries symbol map handle |
| `DbentoPitSymbolMapHandle` | Point-in-time symbol map handle |
| `DbnFileReaderHandle` | DBN file reader handle |
| `DbnFileWriterHandle` | DBN file writer handle |
| `DbentoSymbologyResolutionHandle` | Symbology resolution result handle |
| `DbentoUnitPricesHandle` | Unit prices result handle |

### 8.2 Callback Function Types

```c
// Record callback - invoked for each received record
typedef void (*RecordCallback)(
    const uint8_t* record_bytes,    // Raw DBN record data
    size_t record_length,           // Length in bytes
    uint8_t record_type,            // RType value
    void* user_data                 // User context
);

// Error callback - invoked on errors
typedef void (*ErrorCallback)(
    const char* error_message,      // Error description
    int error_code,                 // Error code (negative = error)
    void* user_data                 // User context
);

// Metadata callback - invoked for session metadata (Phase 15)
typedef void (*MetadataCallback)(
    const char* metadata_json,      // JSON metadata string
    size_t metadata_length,         // Length in bytes
    void* user_data                 // User context
);
```

### 8.3 Live Client Functions

| Function | Parameters | Return | Description |
|----------|------------|--------|-------------|
| `dbento_live_create` | `api_key`, `error_buffer`, `error_buffer_size` | Handle or NULL | Create live client |
| `dbento_live_create_ex` | `api_key`, `dataset`, `send_ts_out`, `upgrade_policy`, `heartbeat_interval_secs`, `error_buffer`, `error_buffer_size` | Handle or NULL | Create with extended config |
| `dbento_live_subscribe` | `handle`, `dataset`, `schema`, `symbols[]`, `symbol_count`, `error_buffer`, `error_buffer_size` | 0 or error | Subscribe to stream |
| `dbento_live_subscribe_with_snapshot` | Same as subscribe | 0 or error | Subscribe with snapshot |
| `dbento_live_subscribe_with_replay` | + `start_time_ns` | 0 or error | Subscribe with replay |
| `dbento_live_start` | `handle`, `on_record`, `on_error`, `user_data`, `error_buffer`, `error_buffer_size` | 0 or error | Start receiving |
| `dbento_live_start_ex` | + `on_metadata` | 0 or error | Start with metadata callback |
| `dbento_live_stop` | `handle` | void | Stop receiving |
| `dbento_live_reconnect` | `handle`, `error_buffer`, `error_buffer_size` | 0 or error | Reconnect |
| `dbento_live_resubscribe` | `handle`, `error_buffer`, `error_buffer_size` | 0 or error | Resubscribe |
| `dbento_live_get_connection_state` | `handle` | 0-3 | Get connection state |
| `dbento_live_destroy` | `handle` | void | Destroy and free |

### 8.4 LiveBlocking Client Functions

| Function | Parameters | Return | Description |
|----------|------------|--------|-------------|
| `dbento_live_blocking_create_ex` | `api_key`, `dataset`, `send_ts_out`, `upgrade_policy`, `heartbeat_interval_secs`, `error_buffer`, `error_buffer_size` | Handle or NULL | Create blocking client |
| `dbento_live_blocking_subscribe` | `handle`, `dataset`, `schema`, `symbols[]`, `symbol_count`, `error_buffer`, `error_buffer_size` | 0 or error | Subscribe |
| `dbento_live_blocking_subscribe_with_replay` | + `start_time_ns` | 0 or error | Subscribe with replay |
| `dbento_live_blocking_subscribe_with_snapshot` | Same as subscribe | 0 or error | Subscribe with snapshot |
| `dbento_live_blocking_start` | `handle`, `metadata_buffer`, `metadata_buffer_size`, `error_buffer`, `error_buffer_size` | 0 or error | Start and get metadata |
| `dbento_live_blocking_next_record` | `handle`, `record_buffer`, `record_buffer_size`, `out_record_length`, `out_record_type`, `timeout_ms`, `error_buffer`, `error_buffer_size` | 0=success, 1=timeout, negative=error | Get next record |
| `dbento_live_blocking_reconnect` | `handle`, `error_buffer`, `error_buffer_size` | 0 or error | Reconnect |
| `dbento_live_blocking_resubscribe` | `handle`, `error_buffer`, `error_buffer_size` | 0 or error | Resubscribe |
| `dbento_live_blocking_stop` | `handle` | void | Stop |
| `dbento_live_blocking_destroy` | `handle` | void | Destroy and free |

### 8.5 Historical Client Functions

| Function | Parameters | Return | Description |
|----------|------------|--------|-------------|
| `dbento_historical_create` | `api_key`, `error_buffer`, `error_buffer_size` | Handle or NULL | Create historical client |
| `dbento_historical_get_range` | `handle`, `dataset`, `schema`, `symbols[]`, `symbol_count`, `start_time_ns`, `end_time_ns`, `on_record`, `user_data`, `error_buffer`, `error_buffer_size` | 0 or error | Query time range |
| `dbento_historical_get_range_to_file` | `handle`, `file_path`, `dataset`, `schema`, `symbols[]`, `symbol_count`, `start_time_ns`, `end_time_ns`, `error_buffer`, `error_buffer_size` | 0 or error | Query to file |
| `dbento_historical_get_range_with_symbology` | + `stype_in`, `stype_out`, `limit` | 0 or error | Query with symbology |
| `dbento_historical_get_range_to_file_with_symbology` | + `stype_in`, `stype_out`, `limit` | 0 or error | Query to file with symbology |
| `dbento_historical_get_metadata` | `handle`, `dataset`, `schema`, `start_time_ns`, `end_time_ns`, `error_buffer`, `error_buffer_size` | Metadata handle or NULL | Get metadata |
| `dbento_historical_destroy` | `handle` | void | Destroy and free |

### 8.6 Metadata Functions

| Function | Parameters | Return | Description |
|----------|------------|--------|-------------|
| `dbento_metadata_get_symbol_mapping` | `handle`, `instrument_id`, `symbol_buffer`, `symbol_buffer_size` | 0 or error | Get symbol for ID |
| `dbento_metadata_destroy` | `handle` | void | Destroy metadata |
| `dbento_metadata_list_datasets` | `handle`, `venue`, `error_buffer`, `error_buffer_size` | JSON string or NULL | List datasets |
| `dbento_metadata_list_publishers` | `handle`, `error_buffer`, `error_buffer_size` | JSON string or NULL | List publishers |
| `dbento_metadata_list_schemas` | `handle`, `dataset`, `error_buffer`, `error_buffer_size` | JSON string or NULL | List schemas |
| `dbento_metadata_list_fields` | `handle`, `encoding`, `schema`, `error_buffer`, `error_buffer_size` | JSON string or NULL | List fields |
| `dbento_metadata_get_dataset_condition` | `handle`, `dataset`, `error_buffer`, `error_buffer_size` | JSON string or NULL | Get condition |
| `dbento_metadata_get_dataset_condition_with_date_range` | `handle`, `dataset`, `start_date`, `end_date`, `error_buffer`, `error_buffer_size` | JSON string or NULL | Get condition for date range |
| `dbento_metadata_get_dataset_range` | `handle`, `dataset`, `error_buffer`, `error_buffer_size` | JSON string or NULL | Get time range |
| `dbento_metadata_get_record_count` | `handle`, `dataset`, `schema`, `start_time_ns`, `end_time_ns`, `symbols[]`, `symbol_count`, `error_buffer`, `error_buffer_size` | uint64 (UINT64_MAX on error) | Get record count |
| `dbento_metadata_get_billable_size` | Same as above | uint64 (UINT64_MAX on error) | Get billable size |
| `dbento_metadata_get_cost` | Same as above | JSON string or NULL | Get cost estimate |
| `dbento_metadata_get_billing_info` | Same as above | JSON string or NULL | Get billing info |

### 8.7 Symbol Map Functions

| Function | Parameters | Return | Description |
|----------|------------|--------|-------------|
| `dbento_metadata_create_symbol_map` | `metadata_handle`, `error_buffer`, `error_buffer_size` | TsSymbolMap handle or NULL | Create TS symbol map |
| `dbento_metadata_create_symbol_map_for_date` | `metadata_handle`, `year`, `month`, `day`, `error_buffer`, `error_buffer_size` | PitSymbolMap handle or NULL | Create PIT symbol map |
| `dbento_ts_symbol_map_is_empty` | `handle` | 1=empty, 0=not empty, -1=error | Check if empty |
| `dbento_ts_symbol_map_size` | `handle` | size_t | Get mapping count |
| `dbento_ts_symbol_map_find` | `handle`, `year`, `month`, `day`, `instrument_id`, `symbol_buffer`, `symbol_buffer_size` | 0 or error | Find symbol |
| `dbento_ts_symbol_map_destroy` | `handle` | void | Destroy |
| `dbento_pit_symbol_map_is_empty` | `handle` | 1=empty, 0=not empty, -1=error | Check if empty |
| `dbento_pit_symbol_map_size` | `handle` | size_t | Get mapping count |
| `dbento_pit_symbol_map_find` | `handle`, `instrument_id`, `symbol_buffer`, `symbol_buffer_size` | 0 or error | Find symbol |
| `dbento_pit_symbol_map_on_record` | `handle`, `record_bytes`, `record_length` | 0 or error | Update from record |
| `dbento_pit_symbol_map_destroy` | `handle` | void | Destroy |

### 8.8 Batch Functions

| Function | Parameters | Return | Description |
|----------|------------|--------|-------------|
| `dbento_batch_submit_job` | `handle`, `dataset`, `schema`, `symbols[]`, `symbol_count`, `start_time_ns`, `end_time_ns`, `error_buffer`, `error_buffer_size` | JSON string or NULL | Submit batch job |
| `dbento_batch_list_jobs` | `handle`, `error_buffer`, `error_buffer_size` | JSON string or NULL | List jobs |
| `dbento_batch_list_files` | `handle`, `job_id`, `error_buffer`, `error_buffer_size` | JSON string or NULL | List files for job |
| `dbento_batch_download_all` | `handle`, `output_dir`, `job_id`, `error_buffer`, `error_buffer_size` | JSON array or NULL | Download all files |
| `dbento_batch_download_file` | `handle`, `output_dir`, `job_id`, `filename`, `error_buffer`, `error_buffer_size` | File path or NULL | Download file |

### 8.9 DBN File I/O Functions

| Function | Parameters | Return | Description |
|----------|------------|--------|-------------|
| `dbento_dbn_file_open` | `file_path`, `error_buffer`, `error_buffer_size` | Reader handle or NULL | Open DBN file |
| `dbento_dbn_file_get_metadata` | `handle`, `error_buffer`, `error_buffer_size` | JSON string or NULL | Get metadata |
| `dbento_dbn_file_next_record` | `handle`, `record_buffer`, `record_buffer_size`, `out_record_length`, `out_record_type`, `error_buffer`, `error_buffer_size` | 0=success, 1=EOF, negative=error | Read next record |
| `dbento_dbn_file_close` | `handle` | void | Close reader |
| `dbento_dbn_file_create` | `file_path`, `metadata_json`, `error_buffer`, `error_buffer_size` | Writer handle or NULL | Create DBN file |
| `dbento_dbn_file_write_record` | `handle`, `record_bytes`, `record_length`, `error_buffer`, `error_buffer_size` | 0 or error | Write record |
| `dbento_dbn_file_close_writer` | `handle` | void | Close writer |

### 8.10 Symbology Resolution Functions

| Function | Parameters | Return | Description |
|----------|------------|--------|-------------|
| `dbento_historical_symbology_resolve` | `handle`, `dataset`, `symbols[]`, `symbol_count`, `stype_in`, `stype_out`, `start_date`, `end_date`, `error_buffer`, `error_buffer_size` | Resolution handle or NULL | Resolve symbols |
| `dbento_symbology_resolution_mappings_count` | `handle` | size_t | Get mappings count |
| `dbento_symbology_resolution_get_mapping_key` | `handle`, `index`, `key_buffer`, `key_buffer_size` | 0 or error | Get mapping key |
| `dbento_symbology_resolution_get_intervals_count` | `handle`, `symbol_key` | size_t | Get intervals count |
| `dbento_symbology_resolution_get_interval` | `handle`, `symbol_key`, `interval_index`, `start_date_buffer`, `start_date_buffer_size`, `end_date_buffer`, `end_date_buffer_size`, `symbol_buffer`, `symbol_buffer_size` | 0 or error | Get interval |
| `dbento_symbology_resolution_partial_count` | `handle` | size_t | Get partial symbols count |
| `dbento_symbology_resolution_get_partial` | `handle`, `index`, `symbol_buffer`, `symbol_buffer_size` | 0 or error | Get partial symbol |
| `dbento_symbology_resolution_not_found_count` | `handle` | size_t | Get not found count |
| `dbento_symbology_resolution_get_not_found` | `handle`, `index`, `symbol_buffer`, `symbol_buffer_size` | 0 or error | Get not found symbol |
| `dbento_symbology_resolution_get_stype_in` | `handle` | int (SType) or -1 | Get input stype |
| `dbento_symbology_resolution_get_stype_out` | `handle` | int (SType) or -1 | Get output stype |
| `dbento_symbology_resolution_destroy` | `handle` | void | Destroy |

### 8.11 Unit Prices Functions

| Function | Parameters | Return | Description |
|----------|------------|--------|-------------|
| `dbento_historical_list_unit_prices` | `handle`, `dataset`, `error_buffer`, `error_buffer_size` | UnitPrices handle or NULL | List unit prices |
| `dbento_unit_prices_get_modes_count` | `handle` | size_t | Get modes count |
| `dbento_unit_prices_get_mode` | `handle`, `mode_index` | int (FeedMode 0-2) or -1 | Get feed mode |
| `dbento_unit_prices_get_schema_count` | `handle`, `mode_index` | size_t | Get schema count |
| `dbento_unit_prices_get_schema_price` | `handle`, `mode_index`, `schema_index`, `out_schema`, `out_price` | 0 or error | Get schema price |
| `dbento_unit_prices_destroy` | `handle` | void | Destroy |

### 8.12 Memory Management

| Function | Parameters | Return | Description |
|----------|------------|--------|-------------|
| `dbento_free_string` | `str` | void | Free string allocated by native library |

---

## Summary Statistics

| Category | Count |
|----------|-------|
| Client Interfaces | 4 (IHistoricalClient, ILiveClient, ILiveBlockingClient, IReferenceClient) |
| Builder Classes | 4 (HistoricalClientBuilder, LiveClientBuilder, LiveBlockingClientBuilder, ReferenceClientBuilder) |
| Record Types | 16 (TradeMessage, MboMessage, Mbp1Message, Mbp10Message, OhlcvMessage, StatusMessage, InstrumentDefMessage, ImbalanceMessage, StatMessage, ErrorMessage, SystemMessage, SymbolMappingMessage, BboMessage, CbboMessage, Cmbp1Message, TcbboMessage) |
| Schemas | 20 |
| Symbology Types | 13 |
| Enumerations | 27 |
| Native Functions | 80+ |
| Total C# Files | ~83 |
| Total C++ Lines | ~3,879 |

---

*Document generated from databento-dotnet v4.0.1-beta source code analysis.*
