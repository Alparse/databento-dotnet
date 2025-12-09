# Databento.Client Backtesting Reference

> **Version**: 5.0+
> **Status**: Production Ready

This document provides a complete reference for backtesting with Databento.Client. The backtesting feature allows you to run trading strategies against historical data using the same code you use for live trading.

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [Downloading Data for Backtesting](#downloading-data-for-backtesting)
3. [Core Concepts](#core-concepts)
4. [BacktestingClientBuilder](#backtestingclientbuilder)
5. [Data Sources](#data-sources)
6. [Playback Control](#playback-control)
7. [Caching](#caching)
8. [Code Parity with Live](#code-parity-with-live)
9. [Complete Examples](#complete-examples)
10. [API Reference](#api-reference)
11. [Symbol Resolution for DBN Files](#symbol-resolution-for-dbn-files)
12. [Best Practices](#best-practices)
13. [Troubleshooting](#troubleshooting)

---

## Quick Start

### Historical Backtesting (API)

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

var start = new DateTimeOffset(2025, 1, 15, 9, 30, 0, TimeSpan.FromHours(-5));
var end = start.AddHours(6.5);  // Full trading day

await using var client = new BacktestingClientBuilder()
    .WithKeyFromEnv()
    .WithTimeRange(start, end)
    .Build();

await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA", "AAPL" });
await client.StartAsync();

await foreach (var record in client.StreamAsync())
{
    if (record is TradeMessage trade)
    {
        Console.WriteLine($"{trade.Timestamp}: {trade.InstrumentId} @ {trade.PriceDecimal}");
    }
}
```

### File-Based Backtesting (Offline)

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

await using var client = new BacktestingClientBuilder()
    .WithFileSource("/path/to/historical_data.dbn")
    .Build();

// No subscription needed - file defines the data
await client.StartAsync();

await foreach (var record in client.StreamAsync())
{
    if (record is TradeMessage trade)
    {
        Console.WriteLine($"{trade.Timestamp}: {trade.PriceDecimal}");
    }
}
```

---

## Downloading Data for Backtesting

Before running file-based backtests, you need to download historical data to a DBN file. This is a one-time operation that saves the data locally for unlimited offline replay.

### Download Trades Data to DBN File

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

// Define the time range (full trading day on January 15, 2025)
var start = new DateTimeOffset(2025, 1, 15, 9, 30, 0, TimeSpan.FromHours(-5));
var end = new DateTimeOffset(2025, 1, 15, 16, 0, 0, TimeSpan.FromHours(-5));

// Create historical client
await using var historicalClient = new HistoricalClientBuilder()
    .WithKeyFromEnv()
    .Build();

// Save to archives folder at project root (not in bin/obj folders)
var archivesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "archives");
archivesDir = Path.GetFullPath(archivesDir);  // Normalize the path
Directory.CreateDirectory(archivesDir);

var outputPath = Path.Combine(archivesDir, "equs_mini_trades_20250115.dbn");
var symbols = new[] { "NVDA", "AAPL", "MSFT", "GOOGL" };

Console.WriteLine($"Downloading {string.Join(", ", symbols)} trades from EQUS.MINI...");
Console.WriteLine($"Time range: {start} to {end}");

var savedPath = await historicalClient.GetRangeToFileAsync(
    outputPath,
    "EQUS.MINI",
    Schema.Trades,
    symbols,
    start,
    end);

Console.WriteLine($"Data saved to: {savedPath}");
```

> **Note:** The `archives` folder is at project root and should be added to `.gitignore` to avoid committing large data files.

### Check Cost Before Downloading

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

var start = new DateTimeOffset(2025, 1, 15, 9, 30, 0, TimeSpan.FromHours(-5));
var end = new DateTimeOffset(2025, 1, 15, 16, 0, 0, TimeSpan.FromHours(-5));
var symbols = new[] { "NVDA", "AAPL", "MSFT", "GOOGL" };

await using var client = new HistoricalClientBuilder()
    .WithKeyFromEnv()
    .Build();

// Get cost estimate before downloading
var billingInfo = await client.GetBillingInfoAsync(
    "EQUS.MINI",
    Schema.Trades,
    start,
    end,
    symbols);

Console.WriteLine($"Record count: {billingInfo.RecordCount:N0}");
Console.WriteLine($"Billable size: {billingInfo.BillableSize / 1024.0 / 1024.0:F2} MB");
Console.WriteLine($"Estimated cost: ${billingInfo.Cost:F4}");

Console.Write("Proceed with download? (y/n): ");
if (Console.ReadLine()?.ToLower() == "y")
{
    var path = await client.GetRangeToFileAsync(
        "equs_mini_trades_20250115.dbn",
        "EQUS.MINI",
        Schema.Trades,
        symbols,
        start,
        end);

    Console.WriteLine($"Downloaded to: {path}");
}
```

### Complete Workflow: Download and Backtest

```csharp
using Databento.Client.Builders;
using Databento.Client.Dbn;
using Databento.Client.Models;
using Databento.Client.Models.Dbn;

// === STEP 1: Download data (run once) ===

var start = new DateTimeOffset(2025, 1, 15, 9, 30, 0, TimeSpan.FromHours(-5));
var end = new DateTimeOffset(2025, 1, 15, 16, 0, 0, TimeSpan.FromHours(-5));
var symbols = new[] { "NVDA", "AAPL" };
var dataFile = "equs_mini_trades_20250115.dbn";

// Check if we need to download
if (!File.Exists(dataFile))
{
    Console.WriteLine("Downloading historical data...");

    await using var historicalClient = new HistoricalClientBuilder()
        .WithKeyFromEnv()
        .Build();

    await historicalClient.GetRangeToFileAsync(
        dataFile,
        "EQUS.MINI",
        Schema.Trades,
        symbols,
        start,
        end);

    Console.WriteLine($"Data saved to {dataFile}");
}
else
{
    Console.WriteLine($"Using cached data from {dataFile}");
}

// === STEP 2: Resolve symbols via API (required - DBN files don't contain mappings) ===

Console.WriteLine("Resolving symbols...");

await using var historicalClient = new HistoricalClientBuilder()
    .WithKeyFromEnv()
    .Build();

// Read metadata from DBN file to get date range
await using var metadataReader = new DbnFileReader(dataFile);
var metadata = metadataReader.GetMetadata();

var startDto = DateTimeOffset.FromUnixTimeMilliseconds(metadata.Start / 1_000_000);
var endDto = DateTimeOffset.FromUnixTimeMilliseconds(metadata.End / 1_000_000);
var startDate = DateOnly.FromDateTime(startDto.DateTime);
var endDate = DateOnly.FromDateTime(endDto.DateTime);

// API requires end > start (add 1 day if same day)
if (endDate <= startDate)
    endDate = startDate.AddDays(1);

// Resolve ALL_SYMBOLS to get complete instrument_id -> ticker mappings
var resolution = await historicalClient.SymbologyResolveAsync(
    metadata.Dataset,
    new[] { "ALL_SYMBOLS" },
    SType.RawSymbol,
    SType.InstrumentId,
    startDate,
    endDate);

// Build reverse map: instrument_id -> ticker
var symbolMap = new Dictionary<uint, string>();
if (resolution?.Mappings != null)
{
    foreach (var (ticker, intervals) in resolution.Mappings)
    {
        foreach (var interval in intervals)
        {
            if (uint.TryParse(interval.Symbol, out var instrumentId))
            {
                symbolMap[instrumentId] = ticker;
            }
        }
    }
}

Console.WriteLine($"Resolved {symbolMap.Count} symbols");

// === STEP 3: Run backtest (can run unlimited times) ===

Console.WriteLine("Running backtest...");

await using var reader = new DbnFileReader(dataFile);
long tradeCount = 0;
decimal totalVolume = 0;

await foreach (var record in reader.ReadRecordsAsync())
{
    if (record is TradeMessage trade)
    {
        var symbol = symbolMap.GetValueOrDefault(trade.InstrumentId, trade.InstrumentId.ToString());
        tradeCount++;
        totalVolume += trade.Size * trade.PriceDecimal;
        // Now you have the ticker symbol!
        // Console.WriteLine($"{symbol}: {trade.PriceDecimal}");
    }
}

Console.WriteLine($"Backtest complete!");
Console.WriteLine($"  Trades processed: {tradeCount:N0}");
Console.WriteLine($"  Total volume: ${totalVolume:N2}");
```

### Download Multiple Schemas

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

var start = new DateTimeOffset(2025, 1, 15, 9, 30, 0, TimeSpan.FromHours(-5));
var end = new DateTimeOffset(2025, 1, 15, 16, 0, 0, TimeSpan.FromHours(-5));
var symbols = new[] { "NVDA" };

await using var client = new HistoricalClientBuilder()
    .WithKeyFromEnv()
    .Build();

// Download different schemas for comprehensive analysis
var schemas = new[]
{
    (Schema.Trades, "nvda_trades_20250115.dbn"),
    (Schema.Mbp1, "nvda_mbp1_20250115.dbn"),
    (Schema.Ohlcv1M, "nvda_ohlcv1m_20250115.dbn")
};

foreach (var (schema, filename) in schemas)
{
    if (!File.Exists(filename))
    {
        Console.WriteLine($"Downloading {schema}...");
        await client.GetRangeToFileAsync(filename, "EQUS.MINI", schema, symbols, start, end);
    }
}

Console.WriteLine("All data downloaded!");
```

---

## Core Concepts

### Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                      Your Trading Code                       │
│              (ILiveClient interface)                         │
└─────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┼───────────────┐
              ▼               ▼               ▼
       ┌──────────┐    ┌──────────────┐  ┌──────────┐
       │LiveClient│    │Backtesting   │  │Backtesting│
       │(live)    │    │Client        │  │Client     │
       │          │    │(historical)  │  │(file)     │
       └──────────┘    └──────────────┘  └──────────┘
              │               │               │
              ▼               ▼               ▼
       ┌──────────┐    ┌──────────────┐  ┌──────────┐
       │Live      │    │Historical    │  │File      │
       │Gateway   │    │API           │  │(DBN)     │
       └──────────┘    └──────────────┘  └──────────┘
```

### Key Interfaces

| Interface | Purpose |
|-----------|---------|
| `ILiveClient` | Common interface for all clients (live and backtest) |
| `IPlaybackControllable` | Access to playback controls (pause/resume/seek) |
| `IDataSource` | Internal abstraction for data sources |

### Design Principles

1. **Code Parity**: Your trading logic works identically in live and backtest modes
2. **Non-Breaking**: Existing code continues to work - backtesting is additive
3. **Flexible**: Choose between API-based or file-based backtesting
4. **Controllable**: Pause, resume, and seek through historical data

---

## BacktestingClientBuilder

The `BacktestingClientBuilder` creates backtesting clients configured for historical or file-based replay.

### Builder Methods

#### Authentication

```csharp
// From environment variable (recommended)
.WithKeyFromEnv()

// Explicit API key
.WithApiKey("db-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")
```

#### Data Source Configuration

```csharp
// Historical API source (requires API key)
.WithTimeRange(startTime, endTime)

// File source (no API key needed)
.WithFileSource("/path/to/data.dbn")
```

#### Playback Configuration

```csharp
// Maximum speed (default) - as fast as possible
.WithPlaybackSpeed(PlaybackSpeed.Maximum)

// Real-time - matches original timestamps
.WithPlaybackSpeed(PlaybackSpeed.RealTime)

// Custom multiplier - 2x, 10x, etc.
.WithPlaybackSpeed(PlaybackSpeed.Times(2.0))
```

#### Caching Configuration

```csharp
// In-memory cache (fast, limited by RAM)
.WithMemoryCache()

// Disk cache (persistent, unlimited replay)
.WithDiskCache()

// Disk cache with custom directory
.WithDiskCache("/custom/cache/path")
```

#### Other Options

```csharp
// Default dataset for subscriptions
.WithDataset("EQUS.MINI")

// Logging
.WithLogger(loggerInstance)
```

### Complete Builder Example

```csharp
var client = new BacktestingClientBuilder()
    .WithKeyFromEnv()
    .WithDataset("EQUS.MINI")
    .WithTimeRange(
        new DateTimeOffset(2025, 1, 15, 9, 30, 0, TimeSpan.FromHours(-5)),
        new DateTimeOffset(2025, 1, 15, 16, 0, 0, TimeSpan.FromHours(-5)))
    .WithPlaybackSpeed(PlaybackSpeed.Times(10))  // 10x speed
    .WithDiskCache()
    .WithLogger(logger)
    .Build();
```

---

## Data Sources

### HistoricalDataSource

Fetches data from Databento's Historical API.

**When to use:**
- First-time backtesting of a specific time period
- When you need the latest historical data
- When disk space is limited (with memory cache)

**Characteristics:**
- Requires API key
- Incurs API costs on first fetch
- Can be cached for repeated replay
- Supports playback speed control
- Supports pause/resume

```csharp
var client = new BacktestingClientBuilder()
    .WithKeyFromEnv()
    .WithTimeRange(start, end)
    .WithDiskCache()  // Cache for repeated runs
    .Build();
```

### FileDataSource

Reads from local DBN files.

**When to use:**
- Offline backtesting (no internet)
- CI/CD testing without API credentials
- Sharing datasets between team members
- Repeated replay of same data

**Characteristics:**
- No API key required
- No API costs
- Fast startup (no network)
- Supports playback speed control
- Supports pause/resume

```csharp
var client = new BacktestingClientBuilder()
    .WithFileSource("/data/nvda_trades_2025.dbn")
    .Build();
```

### LiveDataSource

Wraps the live gateway connection (used internally by `LiveClient`).

**Characteristics:**
- Real-time data
- Requires API key
- No playback control
- Supports reconnection

---

## Playback Control

The `IPlaybackControllable` interface provides control over backtesting playback.

### Checking for Playback Support

```csharp
if (client is IPlaybackControllable controllable)
{
    // Playback control available
    var controller = controllable.Playback;
}
```

### PlaybackController Properties

| Property | Type | Description |
|----------|------|-------------|
| `CurrentIndex` | `long` | Current record index (0-based) |
| `CurrentTimestamp` | `DateTimeOffset?` | Timestamp of last record |
| `IsPaused` | `bool` | Whether playback is paused |
| `IsStopped` | `bool` | Whether playback is stopped |

### PlaybackController Methods

| Method | Description |
|--------|-------------|
| `Pause()` | Pause playback |
| `Resume()` | Resume after pause |
| `Stop()` | Stop playback completely |
| `Reset()` | Reset to beginning |
| `SeekToIndex(long)` | Jump to specific position |

### PlaybackController Events

| Event | Description |
|-------|-------------|
| `Paused` | Fired when playback is paused |
| `Resumed` | Fired when playback is resumed |
| `PositionChanged` | Fired on each record (includes index and timestamp) |

### Playback Control Example

```csharp
using Databento.Client.Builders;
using Databento.Client.DataSources;
using Databento.Client.Live;
using Databento.Client.Models;

// Define time range
var start = new DateTimeOffset(2025, 1, 15, 9, 30, 0, TimeSpan.FromHours(-5));
var end = start.AddHours(6.5);  // Full trading day

await using var client = new BacktestingClientBuilder()
    .WithKeyFromEnv()
    .WithTimeRange(start, end)
    .WithPlaybackSpeed(PlaybackSpeed.RealTime)
    .Build();

await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA" });
await client.StartAsync();

// Get playback controller
var playback = ((IPlaybackControllable)client).Playback;

// Subscribe to position changes
playback.PositionChanged += (s, e) =>
{
    Console.WriteLine($"Position: {e.Index} at {e.Timestamp}");
};

// Stream in background
var streamTask = Task.Run(async () =>
{
    await foreach (var record in client.StreamAsync())
    {
        if (record is TradeMessage trade)
        {
            Console.WriteLine($"Trade: {trade.InstrumentId} @ {trade.PriceDecimal}");
        }
    }
});

// Interactive control
Console.WriteLine("Press P to pause, R to resume, Q to quit");
while (!streamTask.IsCompleted)
{
    if (Console.KeyAvailable)
    {
        var key = Console.ReadKey(true).Key;
        switch (key)
        {
            case ConsoleKey.P:
                playback.Pause();
                Console.WriteLine($"Paused at {playback.CurrentTimestamp}");
                break;
            case ConsoleKey.R:
                playback.Resume();
                Console.WriteLine("Resumed");
                break;
            case ConsoleKey.Q:
                playback.Stop();
                break;
        }
    }
    await Task.Delay(100);
}

await streamTask;
```

---

## Caching

Caching allows you to fetch historical data once and replay it multiple times without additional API costs.

### Cache Policies

| Policy | Storage | Persistence | Best For |
|--------|---------|-------------|----------|
| `None` | N/A | N/A | One-shot analysis |
| `Memory` | RAM | Process lifetime | Small datasets, fast iteration |
| `Disk` | DBN files | Permanent | Large datasets, repeated runs |

### Memory Cache

```csharp
var client = new BacktestingClientBuilder()
    .WithKeyFromEnv()
    .WithTimeRange(start, end)
    .WithMemoryCache()
    .Build();

// First run: fetches from API, caches in memory
await RunBacktest(client);

// To replay: create new client (cache is per-instance)
```

### Disk Cache

```csharp
var client = new BacktestingClientBuilder()
    .WithKeyFromEnv()
    .WithTimeRange(start, end)
    .WithDiskCache()  // Uses default directory
    .Build();

// First run: fetches from API, saves to disk
await RunBacktest(client);

// Subsequent runs: reads from disk (free, fast)
```

### Default Cache Location

| Platform | Location |
|----------|----------|
| Windows | `%LOCALAPPDATA%\Databento\Cache` |
| Linux/macOS | `~/.local/share/Databento/Cache` |

### Cache Key Generation

Cache keys are generated from query parameters:
```
SHA256(dataset + schema + sorted(symbols) + start + end)[0:16]
```

Same query = same cache. Different parameters = different cache.

### Manual Cache Management

```csharp
// Get default cache directory
var cacheDir = DiskRecordCache.GetDefaultCacheDirectory();

// Generate cache key for a query
var cacheKey = DiskRecordCache.GenerateCacheKey(
    "EQUS.MINI",
    Schema.Trades,
    new[] { "NVDA", "AAPL" },
    start,
    end);

// Cache file path
var cachePath = Path.Combine(cacheDir, $"{cacheKey}.dbn");

// Check if cached
if (File.Exists(cachePath))
{
    Console.WriteLine("Using cached data");
}
```

---

## Code Parity with Live

The key benefit of this architecture is that your trading logic works identically in live and backtest modes.

### Writing Mode-Agnostic Code

```csharp
using Databento.Client.Builders;
using Databento.Client.Live;
using Databento.Client.Models;

// Your strategy - works with any ILiveClient
async Task RunStrategy(ILiveClient client)
{
    var symbolMap = new Dictionary<uint, string>();

    await foreach (var record in client.StreamAsync())
    {
        switch (record)
        {
            case SymbolMappingMessage mapping:
                symbolMap[mapping.InstrumentId] = mapping.STypeOutSymbol;
                break;

            case TradeMessage trade:
                var symbol = symbolMap.GetValueOrDefault(trade.InstrumentId, "???");
                Console.WriteLine($"{symbol}: ${trade.PriceDecimal} x {trade.Size}");
                break;
        }
    }
}

// Configuration
var symbols = new[] { "NVDA", "AAPL" };
var start = new DateTimeOffset(2025, 1, 15, 9, 30, 0, TimeSpan.FromHours(-5));
var end = start.AddHours(6.5);
bool isBacktest = true;  // Toggle this for live vs backtest

if (isBacktest)
{
    // Backtesting mode
    await using var client = new BacktestingClientBuilder()
        .WithKeyFromEnv()
        .WithTimeRange(start, end)
        .Build();
    await client.SubscribeAsync("EQUS.MINI", Schema.Trades, symbols);
    await client.StartAsync();
    await RunStrategy(client);  // Same strategy code!
}
else
{
    // Live trading mode
    await using var client = new LiveClientBuilder()
        .WithKeyFromEnv()
        .Build();
    await client.SubscribeAsync("EQUS.MINI", Schema.Trades, symbols);
    await client.StartAsync();
    await RunStrategy(client);  // Same strategy code!
}
```

### Configuration-Driven Mode Switching

```csharp
using Databento.Client.Builders;
using Databento.Client.Live;
using Databento.Client.Models;

// Simple config class
record AppConfig(
    bool BacktestMode,
    DateTimeOffset? BacktestStart,
    DateTimeOffset? BacktestEnd,
    bool UseCache,
    string Dataset,
    Schema Schema,
    string[] Symbols
);

// Factory method creates appropriate client
ILiveClient CreateClient(AppConfig config)
{
    if (config.BacktestMode)
    {
        var builder = new BacktestingClientBuilder()
            .WithKeyFromEnv()
            .WithTimeRange(config.BacktestStart!.Value, config.BacktestEnd!.Value);

        if (config.UseCache)
            builder.WithDiskCache();

        return builder.Build();
    }
    else
    {
        return new LiveClientBuilder()
            .WithKeyFromEnv()
            .WithAutoReconnect()
            .Build();
    }
}

// Example configuration
var config = new AppConfig(
    BacktestMode: true,
    BacktestStart: new DateTimeOffset(2025, 1, 15, 9, 30, 0, TimeSpan.FromHours(-5)),
    BacktestEnd: new DateTimeOffset(2025, 1, 15, 16, 0, 0, TimeSpan.FromHours(-5)),
    UseCache: true,
    Dataset: "EQUS.MINI",
    Schema: Schema.Trades,
    Symbols: new[] { "NVDA", "AAPL" }
);

// Usage - code doesn't know if it's live or backtest
await using var client = CreateClient(config);
await client.SubscribeAsync(config.Dataset, config.Schema, config.Symbols);
await client.StartAsync();

await foreach (var record in client.StreamAsync())
{
    // Process records identically regardless of mode
    if (record is TradeMessage trade)
    {
        Console.WriteLine($"{trade.Timestamp}: {trade.PriceDecimal}");
    }
}
```

---

## Complete Examples

### Example 1: Simple Backtest

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

var start = new DateTimeOffset(2025, 1, 15, 9, 30, 0, TimeSpan.FromHours(-5));
var end = new DateTimeOffset(2025, 1, 15, 16, 0, 0, TimeSpan.FromHours(-5));

await using var client = new BacktestingClientBuilder()
    .WithKeyFromEnv()
    .WithTimeRange(start, end)
    .Build();

await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA" });
var metadata = await client.StartAsync();

Console.WriteLine($"Backtesting {metadata.Dataset} from {start} to {end}");

long tradeCount = 0;
decimal totalVolume = 0;

await foreach (var record in client.StreamAsync())
{
    if (record is TradeMessage trade)
    {
        tradeCount++;
        totalVolume += trade.Size * trade.PriceDecimal;
    }
}

Console.WriteLine($"Processed {tradeCount:N0} trades, ${totalVolume:N2} volume");
```

### Example 2: Cached Multi-Run Backtest

```csharp
using Databento.Client.Builders;
using Databento.Client.Live;
using Databento.Client.Models;

// Define time range and symbols
var start = new DateTimeOffset(2025, 1, 15, 9, 30, 0, TimeSpan.FromHours(-5));
var end = start.AddHours(6.5);
var symbols = new[] { "NVDA", "AAPL" };

// Run backtest with given threshold parameter
async Task<decimal> RunBacktest(ILiveClient client, decimal priceThreshold)
{
    decimal totalVolume = 0;

    await foreach (var record in client.StreamAsync())
    {
        if (record is TradeMessage trade && trade.PriceDecimal > priceThreshold)
        {
            totalVolume += trade.Size * trade.PriceDecimal;
        }
    }

    return totalVolume;
}

// Test different price thresholds
var thresholds = new[] { 100m, 125m, 150m, 175m };

foreach (var threshold in thresholds)
{
    // Each iteration uses cached data (first run fetches from API)
    await using var client = new BacktestingClientBuilder()
        .WithKeyFromEnv()
        .WithTimeRange(start, end)
        .WithDiskCache()  // Cache for repeated runs
        .Build();

    await client.SubscribeAsync("EQUS.MINI", Schema.Trades, symbols);
    await client.StartAsync();

    var volume = await RunBacktest(client, threshold);
    Console.WriteLine($"Threshold: ${threshold} -> Volume: ${volume:N2}");
}
```

### Example 3: Interactive Debugging with Playback Control

```csharp
using Databento.Client.Builders;
using Databento.Client.DataSources;
using Databento.Client.Live;
using Databento.Client.Models;

var start = new DateTimeOffset(2025, 1, 15, 9, 30, 0, TimeSpan.FromHours(-5));
var end = start.AddHours(6.5);

await using var client = new BacktestingClientBuilder()
    .WithKeyFromEnv()
    .WithTimeRange(start, end)
    .WithPlaybackSpeed(PlaybackSpeed.RealTime)
    .Build();

await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA" });
await client.StartAsync();

var playback = ((IPlaybackControllable)client).Playback;

// Pause at specific condition
await foreach (var record in client.StreamAsync())
{
    if (record is TradeMessage trade)
    {
        if (trade.PriceDecimal > 150.00m)
        {
            playback.Pause();
            Console.WriteLine($"Large price detected at {trade.Timestamp}");
            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
            playback.Resume();
        }
    }
}
```

### Example 4: File-Based CI/CD Testing

```csharp
using Databento.Client.Builders;
using Databento.Client.DataSources;
using Databento.Client.Models;
using NUnit.Framework;

[Test]
public async Task Strategy_ShouldProcessAllRecords()
{
    // Use pre-downloaded test data file (no API key needed)
    await using var client = new BacktestingClientBuilder()
        .WithFileSource("TestData/nvda_trades_sample.dbn")
        .WithPlaybackSpeed(PlaybackSpeed.Maximum)
        .Build();

    await client.StartAsync();

    long recordCount = 0;

    await foreach (var record in client.StreamAsync())
    {
        recordCount++;

        if (record is TradeMessage trade)
        {
            // Process trade
            Assert.That(trade.PriceDecimal, Is.GreaterThan(0));
        }
    }

    Assert.That(recordCount, Is.GreaterThan(0));
}
```

---

## API Reference

### BacktestingClientBuilder

```csharp
public sealed class BacktestingClientBuilder
{
    BacktestingClientBuilder WithApiKey(string apiKey);
    BacktestingClientBuilder WithKeyFromEnv();
    BacktestingClientBuilder WithDataset(string dataset);
    BacktestingClientBuilder WithTimeRange(DateTimeOffset start, DateTimeOffset end);
    BacktestingClientBuilder WithFileSource(string filePath);
    BacktestingClientBuilder WithPlaybackSpeed(PlaybackSpeed speed);
    BacktestingClientBuilder WithMemoryCache();
    BacktestingClientBuilder WithDiskCache(string? directory = null);
    BacktestingClientBuilder WithLogger(ILogger logger);
    ILiveClient Build();
}
```

### PlaybackSpeed

```csharp
public readonly struct PlaybackSpeed
{
    // Factory methods
    static PlaybackSpeed Maximum { get; }      // As fast as possible
    static PlaybackSpeed RealTime { get; }     // 1:1 with timestamps
    static PlaybackSpeed Times(double multiplier);  // Custom speed

    // Properties
    double Multiplier { get; }
    bool IsMaximum { get; }

    // Methods
    TimeSpan CalculateDelay(long previousNanos, long currentNanos);
}
```

### PlaybackController

```csharp
public sealed class PlaybackController
{
    // Properties
    long CurrentIndex { get; }
    DateTimeOffset? CurrentTimestamp { get; }
    bool IsPaused { get; }
    bool IsStopped { get; }

    // Methods
    void Pause();
    void Resume();
    void Stop();
    void Reset();
    void SeekToIndex(long index);
    long GetResumeIndex();

    // Events
    event EventHandler? Paused;
    event EventHandler? Resumed;
    event EventHandler<PlaybackPositionEventArgs>? PositionChanged;
}
```

### IPlaybackControllable

```csharp
public interface IPlaybackControllable
{
    PlaybackController Playback { get; }
}
```

### DataSourceCapabilities

```csharp
public sealed record DataSourceCapabilities
{
    bool SupportsReconnect { get; }
    bool SupportsSnapshot { get; }
    bool SupportsReplay { get; }
    bool IsRealTime { get; }
    bool SupportsPlaybackSpeed { get; }
    bool SupportsPauseResume { get; }

    // Predefined capabilities
    static DataSourceCapabilities Live { get; }
    static DataSourceCapabilities Historical { get; }
    static DataSourceCapabilities HistoricalCached { get; }
    static DataSourceCapabilities File { get; }
}
```

---

## Symbol Resolution for DBN Files

> **CRITICAL**: DBN files downloaded from Databento do NOT contain symbol mappings. Trades only have `InstrumentId` (uint), not ticker symbols. You MUST resolve symbols via the Symbology API.

### Why Symbol Resolution is Needed

When you download historical data to a DBN file, the file contains:
- Trade records with `InstrumentId` (uint) - e.g., `12345`
- **NO** `SymbolMappingMessage` records in the stream
- Metadata with `Symbols: ["ALL_SYMBOLS"]` or the symbols you requested
- Metadata with `Mappings: []` (empty!)

This is true for ALL DBN file downloads, regardless of how many symbols you request.

### The Solution: SymbologyResolveAsync

Use the Historical API's `SymbologyResolveAsync` to build a mapping from `InstrumentId` to ticker symbol:

```csharp
using Databento.Client.Builders;
using Databento.Client.Dbn;
using Databento.Client.Models;
using Databento.Client.Models.Dbn;

// 1. Read metadata from DBN file
await using var reader = new DbnFileReader("trades.dbn");
var metadata = reader.GetMetadata();

// 2. Get date range (convert nanoseconds to DateOnly)
var startDto = DateTimeOffset.FromUnixTimeMilliseconds(metadata.Start / 1_000_000);
var endDto = DateTimeOffset.FromUnixTimeMilliseconds(metadata.End / 1_000_000);
var startDate = DateOnly.FromDateTime(startDto.DateTime);
var endDate = DateOnly.FromDateTime(endDto.DateTime);

// IMPORTANT: API requires end_date > start_date
// Single-day files have same start/end, so add 1 day
if (endDate <= startDate)
    endDate = startDate.AddDays(1);

// 3. Resolve ALL_SYMBOLS to get complete mappings
await using var historicalClient = new HistoricalClientBuilder()
    .WithKeyFromEnv()
    .Build();

var resolution = await historicalClient.SymbologyResolveAsync(
    metadata.Dataset,           // e.g., "EQUS.MINI"
    new[] { "ALL_SYMBOLS" },    // Get ALL symbols in the dataset
    SType.RawSymbol,            // Input: ticker symbols
    SType.InstrumentId,         // Output: instrument IDs
    startDate,
    endDate);

// 4. Build reverse map: instrument_id -> ticker
var symbolMap = new Dictionary<uint, string>();
if (resolution?.Mappings != null)
{
    foreach (var (ticker, intervals) in resolution.Mappings)
    {
        foreach (var interval in intervals)
        {
            if (uint.TryParse(interval.Symbol, out var instrumentId))
            {
                symbolMap[instrumentId] = ticker;
            }
        }
    }
}

Console.WriteLine($"Resolved {symbolMap.Count} symbols");

// 5. Now process trades with symbols
await foreach (var record in reader.ReadRecordsAsync())
{
    if (record is TradeMessage trade)
    {
        var symbol = symbolMap.GetValueOrDefault(trade.InstrumentId, "UNKNOWN");
        Console.WriteLine($"{symbol}: {trade.PriceDecimal}");
    }
}
```

### Common Pitfalls

| Pitfall | Problem | Solution |
|---------|---------|----------|
| Expecting SymbolMappingMessage | DBN files don't contain them | Use SymbologyResolveAsync API |
| Resolving individual instrument IDs | API has 2000 symbol limit | Use `["ALL_SYMBOLS"]` instead |
| Same start/end date | API requires end > start | Add 1 day to end date |
| Using DateTimeOffset for API | API expects DateOnly | Convert using DateOnly.FromDateTime() |

### Best Practice: Two-Pass Processing

For backtest services, use a two-pass approach:

1. **Pass 1**: Resolve symbols via API before processing
2. **Pass 2**: Stream trades and look up symbols from the pre-built map

This is more efficient than trying to resolve symbols during streaming.

---

## Best Practices

### 1. Use Disk Cache for Repeated Backtests

```csharp
// Good - data fetched once, reused many times
.WithDiskCache()

// Expensive - fetches from API every run
// (no caching)
```

### 2. Use Maximum Speed Unless Testing Timing

```csharp
// Good for most backtests
.WithPlaybackSpeed(PlaybackSpeed.Maximum)

// Only when you need to simulate real-time behavior
.WithPlaybackSpeed(PlaybackSpeed.RealTime)
```

### 3. Check for Playback Support Before Using

```csharp
// Good
if (client is IPlaybackControllable controllable)
{
    controllable.Playback.Pause();
}

// Bad - may throw if not supported
((IPlaybackControllable)client).Playback.Pause();
```

### 4. Resolve Symbols Before Processing DBN Files

```csharp
// IMPORTANT: DBN files don't contain SymbolMappingMessage records!
// You must resolve symbols via the Symbology API first.

// See the "Symbol Resolution for DBN Files" section above for the full pattern.
// Key steps:
// 1. Read metadata from DBN file to get dataset and date range
// 2. Call SymbologyResolveAsync with ["ALL_SYMBOLS"]
// 3. Build map: instrument_id -> ticker
// 4. Then process trades using the pre-built map

var symbol = symbolMap.GetValueOrDefault(trade.InstrumentId, "UNKNOWN");
```

### 5. Use File Sources for CI/CD

```csharp
// CI/CD friendly - no API key needed, deterministic
.WithFileSource("TestData/sample.dbn")
```

---

## Troubleshooting

### "API key is required for historical backtesting"

```csharp
// Problem: Missing API key
new BacktestingClientBuilder()
    .WithTimeRange(start, end)
    .Build();  // Throws!

// Solution: Add API key
new BacktestingClientBuilder()
    .WithKeyFromEnv()  // or .WithApiKey("...")
    .WithTimeRange(start, end)
    .Build();
```

### "Either WithTimeRange() or WithFileSource() must be called"

```csharp
// Problem: No data source configured
new BacktestingClientBuilder()
    .WithKeyFromEnv()
    .Build();  // Throws!

// Solution: Specify data source
new BacktestingClientBuilder()
    .WithKeyFromEnv()
    .WithTimeRange(start, end)  // or .WithFileSource(...)
    .Build();
```

### "Playback control is not supported by this data source"

```csharp
// Problem: Trying to access playback on live client
var liveClient = new LiveClientBuilder().WithKeyFromEnv().Build();
((IPlaybackControllable)liveClient).Playback.Pause();  // Throws!

// Solution: Check interface first
if (client is IPlaybackControllable controllable)
{
    controllable.Playback.Pause();
}
```

### "DBN file not found"

```csharp
// Problem: File doesn't exist
new BacktestingClientBuilder()
    .WithFileSource("/nonexistent/file.dbn")  // Throws!
    .Build();

// Solution: Verify file path
var path = "/path/to/data.dbn";
if (!File.Exists(path))
{
    Console.WriteLine($"File not found: {path}");
}
```

### Slow Backtest Performance

1. Use `PlaybackSpeed.Maximum` (default)
2. Enable disk cache to avoid re-fetching
3. Reduce symbol count if possible
4. Check if you're doing expensive operations per-record

### "Symbols not resolving" / Only seeing InstrumentId numbers

```csharp
// Problem: Expecting SymbolMappingMessage in DBN file stream
await foreach (var record in reader.ReadRecordsAsync())
{
    if (record is SymbolMappingMessage mapping)  // Never hits!
    {
        symbolMap[mapping.InstrumentId] = mapping.STypeOutSymbol;
    }
}

// Solution: DBN files don't contain symbol mappings.
// Use SymbologyResolveAsync BEFORE processing the file.
// See "Symbol Resolution for DBN Files" section above.
```

### "exceeds maximum limit of 2,000 symbols"

```csharp
// Problem: Trying to resolve individual instrument IDs
var instrumentIds = new[] { "12345", "12346", "12347", ... };  // 4000+ IDs
await historicalClient.SymbologyResolveAsync(
    dataset, instrumentIds, SType.InstrumentId, SType.RawSymbol, ...);  // Throws!

// Solution: Use ALL_SYMBOLS with RawSymbol -> InstrumentId direction
await historicalClient.SymbologyResolveAsync(
    dataset,
    new[] { "ALL_SYMBOLS" },  // Gets all symbols in one call
    SType.RawSymbol,          // Input: tickers
    SType.InstrumentId,       // Output: instrument IDs
    startDate,
    endDate);
```

### "start_date cannot be on or after end_date"

```csharp
// Problem: Single-day DBN file has same start and end date
var startDate = DateOnly.FromDateTime(startDto.DateTime);  // 2025-01-15
var endDate = DateOnly.FromDateTime(endDto.DateTime);      // 2025-01-15

await historicalClient.SymbologyResolveAsync(
    dataset, symbols, sTypeIn, sTypeOut, startDate, endDate);  // Throws!

// Solution: Add 1 day to end date
if (endDate <= startDate)
    endDate = startDate.AddDays(1);
```

---

## See Also

- [Main README](../readme_for_coding_agents.md) - General library reference
- [Implementation Plan](design/BACKTESTING_ARCH2_IMPLEMENTATION.md) - Architecture details
- [Databento API Documentation](https://docs.databento.com/) - Official API docs
