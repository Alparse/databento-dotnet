# databento-dotnet

[![NuGet](https://img.shields.io/badge/NuGet-v4.1.1-blue)](https://www.nuget.org/packages/Databento.Client)
[![Downloads](https://img.shields.io/badge/Downloads-6.4K-blue)](https://www.nuget.org/packages/Databento.Client)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0-purple)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)

A high-performance .NET client for [Databento](https://databento.com) market data. Stream real-time data or query historical records with async/await and IAsyncEnumerable.

## Table of Contents

- [Installation](#installation)
- [Quick Start](#quick-start)
- [Features](#features)
- [Symbol Mapping](#symbol-mapping)
- [API Reference](#api-reference)
- [Building from Source](#building-from-source)
- [Troubleshooting](#troubleshooting)
- [License](#license)

## Installation

```bash
dotnet add package Databento.Client
```

**Requirements:** .NET 8.0 or .NET 9.0

**Platforms:** Windows (x64), Linux (x64), macOS (x64/ARM64)

## Quick Start

### 1. Set Your API Key

```bash
# Windows
$env:DATABENTO_API_KEY="your-api-key"

# Linux/macOS
export DATABENTO_API_KEY="your-api-key"
```

Get your API key at [databento.com/portal/keys](https://databento.com/portal/keys)

### 2. Live Streaming

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

await using var client = new LiveClientBuilder()
    .WithKeyFromEnv()
    .WithAutoReconnect()  // Auto-reconnect on failure
    .Build();

// Live mode (requires market hours)
await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA" });

// Intraday Replay mode - replays from most recent market open, then continues live:
// (only available within 24h of last market open)
// await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA" },
//     startTime: DateTimeOffset.MinValue);

await client.StartAsync();

await foreach (var record in client.StreamAsync())
{
    if (record is TradeMessage trade)
        Console.WriteLine($"{trade.InstrumentId}: ${trade.PriceDecimal} x {trade.Size}");
}
```

### 3. Historical Data

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

await using var client = new HistoricalClientBuilder()
    .WithKeyFromEnv()
    .Build();

var start = new DateTimeOffset(2025, 1, 15, 14, 30, 0, TimeSpan.Zero);
var end = start.AddHours(1);

await foreach (var record in client.GetRangeAsync(
    "EQUS.MINI", Schema.Trades, new[] { "NVDA" }, start, end))
{
    Console.WriteLine(record);
}
```

## Features

| Feature | Description |
|---------|-------------|
| **Live Streaming** | Real-time market data with async/await and IAsyncEnumerable |
| **Historical Queries** | Time-range queries with efficient streaming |
| **Auto-Reconnect** | Configurable retry policies with exponential backoff |
| **All Record Types** | Full support for all 16 DBN record types |
| **Symbol Mapping** | Resolve InstrumentId to ticker symbols |
| **Reference Data** | SecurityMaster, CorporateActions, AdjustmentFactors |
| **Cross-Platform** | Windows, Linux, macOS |
| **High Performance** | Built on databento-cpp with native P/Invoke |

### Supported Schemas (20)

`MBO` · `MBP-1` · `MBP-10` · `TBBO` · `Trades` · `OHLCV-1S` · `OHLCV-1M` · `OHLCV-1H` · `OHLCV-1D` · `OHLCV-EOD` · `Definition` · `Statistics` · `Status` · `Imbalance` · `CMBP-1` · `CBBO-1S` · `CBBO-1M` · `TCBBO` · `BBO-1S` · `BBO-1M`

## Symbol Mapping

Records contain numeric `InstrumentId` values instead of ticker symbols. Resolve them as shown below.

### Live Client Symbol Mapping

```csharp
using System.Collections.Concurrent;
using Databento.Client.Builders;
using Databento.Client.Models;

var symbolMap = new ConcurrentDictionary<uint, string>();

await using var client = new LiveClientBuilder()
    .WithKeyFromEnv()
    .Build();

client.DataReceived += (sender, e) =>
{
    if (e.Record is SymbolMappingMessage mapping)
    {
        // Use STypeOutSymbol (NOT STypeInSymbol)
        symbolMap[mapping.InstrumentId] = mapping.STypeOutSymbol;
        Console.WriteLine($"Mapped {mapping.InstrumentId} to {mapping.STypeOutSymbol}");
        return;
    }

    if (e.Record is TradeMessage trade)
    {
        var symbol = symbolMap.GetValueOrDefault(trade.InstrumentId, "UNKNOWN");
        Console.WriteLine($"{symbol}: ${trade.PriceDecimal}");
    }
};

await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA", "AAPL" });
await client.StartAsync();
await foreach (var record in client.StreamAsync()) { }
```

> **Important:** Always use `STypeOutSymbol` for the actual ticker. `STypeInSymbol` contains your subscription string (e.g., "ALL_SYMBOLS").

### LiveBlocking Client Symbol Mapping

```csharp
using System.Collections.Concurrent;
using Databento.Client.Builders;
using Databento.Client.Models;

var symbolMap = new ConcurrentDictionary<uint, string>();

await using var client = new LiveBlockingClientBuilder()
    .WithKeyFromEnv()
    .WithDataset("EQUS.MINI")
    .Build();

await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA", "AAPL" });
await client.StartAsync();

while (true)
{
    var record = await client.NextRecordAsync(timeout: TimeSpan.FromSeconds(5));
    if (record == null) break;

    if (record is SymbolMappingMessage mapping)
    {
        symbolMap[mapping.InstrumentId] = mapping.STypeOutSymbol;
        Console.WriteLine($"Mapped {mapping.InstrumentId} to {mapping.STypeOutSymbol}");
        continue;
    }

    if (record is TradeMessage trade)
    {
        var symbol = symbolMap.GetValueOrDefault(trade.InstrumentId, "UNKNOWN");
        Console.WriteLine($"{symbol}: ${trade.PriceDecimal}");
    }
}
```

### Historical Client Symbol Mapping

> **Note:** Historical API does **not** send SymbolMappingMessage. Use `SymbologyResolveAsync()` first.

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

await using var client = new HistoricalClientBuilder()
    .WithKeyFromEnv()
    .Build();

var symbols = new[] { "NVDA", "AAPL" };
var start = new DateTimeOffset(2025, 1, 15, 14, 30, 0, TimeSpan.Zero);
var end = start.AddHours(1);

// Step 1: Resolve symbols to instrument IDs BEFORE streaming
var queryDate = DateOnly.FromDateTime(start.Date);
var resolution = await client.SymbologyResolveAsync(
    "EQUS.MINI", symbols, SType.RawSymbol, SType.InstrumentId,
    queryDate, queryDate.AddDays(1));

var symbolMap = new Dictionary<uint, string>();
foreach (var (inputSymbol, intervals) in resolution.Mappings)
    foreach (var interval in intervals)
        if (uint.TryParse(interval.Symbol, out var instrumentId))
        {
            symbolMap[instrumentId] = inputSymbol;
            Console.WriteLine($"Mapped {instrumentId} to {inputSymbol}");
        }

// Step 2: Stream data using the pre-built symbol map
await foreach (var record in client.GetRangeAsync(
    "EQUS.MINI", Schema.Trades, symbols, start, end))
{
    if (record is TradeMessage trade)
    {
        var symbol = symbolMap.GetValueOrDefault(trade.InstrumentId, "UNKNOWN");
        Console.WriteLine($"{symbol}: ${trade.PriceDecimal} x {trade.Size}");
    }
}
```

## API Reference

### LiveClient

```csharp
await using var client = new LiveClientBuilder()
    .WithApiKey(apiKey)           // Or .WithKeyFromEnv()
    .WithDataset("GLBX.MDP3")     // Default dataset
    .WithAutoReconnect()          // Enable resilience
    .Build();

// Subscribe and stream
await client.SubscribeAsync(dataset, schema, symbols);
await client.StartAsync();
await foreach (var record in client.StreamAsync()) { }

// Events
client.DataReceived += (s, e) => { };
client.ErrorOccurred += (s, e) => { };
```

### LiveBlockingClient

Pull-based API for explicit control over record retrieval:

```csharp
await using var client = new LiveBlockingClientBuilder()
    .WithKeyFromEnv()
    .WithDataset("EQUS.MINI")
    .Build();

await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA" });
await client.StartAsync();

// Pull records one at a time
while (true)
{
    var record = await client.NextRecordAsync(timeout: TimeSpan.FromSeconds(5));
    if (record == null) break;  // Timeout reached
    Console.WriteLine(record);
}
```

### HistoricalClient

```csharp
await using var client = new HistoricalClientBuilder()
    .WithKeyFromEnv()
    .Build();

await foreach (var record in client.GetRangeAsync(
    dataset, schema, symbols, startTime, endTime)) { }
```

### ReferenceClient

```csharp
var client = new ReferenceClientBuilder()
    .WithKeyFromEnv()  // Or .WithApiKey(apiKey)
    .Build();

// Security master
var records = await client.SecurityMaster.GetLastAsync(
    symbols: new[] { "NVDA" }, stypeIn: SType.RawSymbol);

// Adjustment factors
var adjustments = await client.AdjustmentFactors.GetRangeAsync(
    start: DateTimeOffset.UtcNow.AddDays(-90), symbols: new[] { "NVDA" });

// Corporate actions
var actions = await client.CorporateActions.GetRangeAsync(
    start: DateTimeOffset.UtcNow.AddYears(-1), symbols: new[] { "NVDA" });
```

### Resilience & Auto-Reconnect

The client includes built-in resilience features for production deployments:

```csharp
using Databento.Client.Builders;
using Databento.Client.Resilience;

await using var client = new LiveClientBuilder()
    .WithKeyFromEnv()
    .WithAutoReconnect()                              // Enable auto-reconnect
    .WithRetryPolicy(RetryPolicy.Aggressive)          // 5 retries, longer delays
    .WithHeartbeatTimeout(TimeSpan.FromSeconds(60))   // Stale connection detection
    .Build();
```

#### Retry Policies

| Policy | Max Retries | Initial Delay | Max Delay |
|--------|-------------|---------------|-----------|
| `RetryPolicy.Default` | 3 | 1s | 30s |
| `RetryPolicy.Aggressive` | 5 | 1s | 60s |
| `RetryPolicy.None` | 0 | - | - |

#### Resilience Callbacks

```csharp
using Databento.Client.Builders;
using Databento.Client.Resilience;

var options = new ResilienceOptions
{
    AutoReconnect = true,
    OnReconnecting = (attempt, ex) => {
        Console.WriteLine($"Reconnecting (attempt {attempt}): {ex.Message}");
        return true;  // Continue reconnecting
    },
    OnReconnected = (attempts) => Console.WriteLine($"Reconnected after {attempts} attempts"),
    OnReconnectFailed = (ex) => Console.WriteLine($"Reconnect failed: {ex.Message}")
};

await using var client = new LiveClientBuilder()
    .WithKeyFromEnv()
    .WithResilienceOptions(options)
    .Build();
```

## Building from Source

### Prerequisites

- .NET 8 SDK or later
- CMake 3.24+
- C++17 compiler (VS 2019+, GCC 9+, Clang 10+)

### Build

```bash
# Full build (native + .NET)
./build/build-all.ps1 -Configuration Release    # Windows
./build/build-all.sh --configuration Release    # Linux/macOS

# .NET only (if native library already built)
dotnet build -c Release
```

### Project Structure

```
databento-dotnet/
├── src/
│   ├── Databento.Client/     # High-level .NET API
│   ├── Databento.Interop/    # P/Invoke layer
│   └── Databento.Native/     # C++ wrapper (CMake)
├── examples/                  # 25+ working examples
└── docs/                      # Additional documentation
```

## Troubleshooting

### DllNotFoundException

The NuGet package includes all dependencies. If you still see this error:

1. Ensure you're on a supported platform (Windows x64, Linux x64, macOS x64/ARM64)
2. Try `dotnet restore --force`
3. On Windows, install [VC++ 2022 Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe)

### Connection Issues

```csharp
// Enable logging for diagnostics
var client = new LiveClientBuilder()
    .WithKeyFromEnv()
    .WithLogger(loggerFactory.CreateLogger<ILiveClient>())
    .Build();
```

### More Help

- [API Reference](API_REFERENCE.md) - Quick-start guide with examples
- [API Classification](API_Classification.md) - Complete method signatures
- [Databento Documentation](https://databento.com/docs/)
- [Issue Tracker](https://github.com/Alparse/databento-dotnet/issues)

## License

Apache 2.0 - See [LICENSE](LICENSE)

---

Built on [databento-cpp](https://github.com/databento/databento-cpp)
