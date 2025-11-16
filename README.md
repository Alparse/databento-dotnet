# databento-dotnet

A high-performance .NET client for accessing [Databento](https://databento.com) market data, supporting both real-time streaming and historical data queries.

> ⚠️ **Beta Software**: This is a newly developed client library. While functional and tested, it should be thoroughly validated in your specific use case before production deployment. Please report any issues to the [issue tracker](https://github.com/Alparse/databento-dotnet/issues).

## Features

- **Live Streaming**: Real-time market data with async/await and IAsyncEnumerable support
- **Historical Data**: Query past market data with time-range filtering
- **Cross-Platform**: Works on Windows, Linux, and macOS
- **High Performance**: Built on top of Databento's C++ client library
- **Type-Safe**: Strongly-typed API with full IntelliSense support
- **.NET 8**: Modern C# with nullable reference types

## Implementation Status

### Record Type Coverage: 100% ✅

All 16 DBN record types from databento-cpp are fully implemented:

| Record Type | Status | Size | Description |
|------------|--------|------|-------------|
| TradeMessage | ✅ | 48 bytes | Trades (RType 0x00) |
| MboMessage | ✅ | 56 bytes | Market by Order (RType 0xA0) |
| Mbp1Message | ✅ | 80 bytes | Market by Price Level 1 (RType 0x01) |
| Mbp10Message | ✅ | 368 bytes | Market by Price Level 10 (RType 0x0A) |
| OhlcvMessage | ✅ | 56 bytes | OHLCV bars - deprecated, 1s, 1m, 1h, 1d, EOD (RType 0x11, 0x20-0x24) |
| StatusMessage | ✅ | 40 bytes | Trading status (RType 0x12) |
| InstrumentDefMessage | ✅ | 520 bytes | Instrument definitions (RType 0x13) |
| ImbalanceMessage | ✅ | 112 bytes | Order imbalances (RType 0x14) |
| ErrorMessage | ✅ | 320 bytes | Error messages (RType 0x15) |
| SymbolMappingMessage | ✅ | 176 bytes | Symbol mappings (RType 0x16) |
| SystemMessage | ✅ | 320 bytes | System messages & heartbeats (RType 0x17) |
| StatMessage | ✅ | 80 bytes | Market statistics (RType 0x18) |
| BboMessage | ✅ | 80 bytes | Best Bid/Offer - 1s, 1m (RType 0xC3-0xC4) |
| CbboMessage | ✅ | 80 bytes | Consolidated BBO - 1s, 1m (RType 0xC0-0xC1) |
| Cmbp1Message | ✅ | 80 bytes | Consolidated Market by Price (RType 0xB1) |
| TcbboMessage | ✅ | 80 bytes | Trade with Consolidated BBO (RType 0xC2) |
| UnknownRecord | ✅ | Variable | Fallback for unrecognized types |

### API Coverage

| Feature | databento-cpp | databento-dotnet | Status |
|---------|---------------|---------------|--------|
| **Live Streaming** | ✅ | ✅ | Complete |
| Subscribe to datasets | ✅ | ✅ | Complete |
| Multiple symbol subscription | ✅ | ✅ | Complete |
| Schema filtering | ✅ | ✅ | Complete |
| Start/Stop streaming | ✅ | ✅ | Complete |
| **Record Deserialization** | ✅ | ✅ | Complete |
| All 16 record types | ✅ | ✅ | Complete |
| Fixed-point price conversion | ✅ | ✅ | Complete |
| Timestamp handling | ✅ | ✅ | Complete |
| **Helper Utilities** | ✅ | ✅ | Complete |
| FlagSet (bit flags) | ✅ | ✅ | Complete |
| Constants & sentinel values | ✅ | ✅ | Complete |
| Schema enums | ✅ | ✅ | Complete |
| **Historical Client** | ✅ | ✅ | Complete (time-range queries) |
| Time-range queries | ✅ | ✅ | Complete with IAsyncEnumerable |
| Batch downloads | ✅ | ❌ | Not yet implemented |
| **Metadata & Symbol Mapping** | ✅ | ✅ | Complete |
| Instrument metadata queries | ✅ | ✅ | 10+ metadata API methods working |
| Symbol resolution | ✅ | ✅ | Full TsSymbolMap & PitSymbolMap support |
| **Advanced Features** | | | |
| Compression (zstd) | ✅ | ✅ | Handled by native layer |
| SSL/TLS | ✅ | ✅ | Handled by native layer |
| Reconnection logic | ✅ | ⚠️ | Delegated to databento-cpp |

### Implementation Statistics

- **Total Record Types**: 16/16 (100%)
- **Enumerations**: 11/11 (100%)
  - Schema, RType, Action, Side, InstrumentClass, MatchAlgorithm, UserDefinedInstrument, SecurityUpdateAction, StatType, StatUpdateAction, SType
- **Helper Classes**: 3/3 (100%)
  - FlagSet, Constants, BidAskPair/ConsolidatedBidAskPair
- **Live Streaming**: Fully functional
- **Binary Deserialization**: All DBN formats supported
- **Lines of Code**: ~2,500 in high-level API, ~500 in P/Invoke layer, ~800 in native wrapper

### Recent Changes

**Latest (November 2025)** - Production Ready
- ✅ Reference API implementation (SecurityMaster, AdjustmentFactors, CorporateActions)
- ✅ OpenTelemetry telemetry with retry policies
- ✅ Complete metadata & symbol mapping (TsSymbolMap, PitSymbolMap)
- ✅ All 16 record types with proper deserialization
- ✅ Thread-safe LiveClient with reconnection support

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Your .NET Application                     │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│           Databento.Client (High-Level API)                  │
│   • LiveClient, HistoricalClient                            │
│   • Async/await, IAsyncEnumerable, Events                   │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│        Databento.Interop (P/Invoke Layer)                   │
│   • SafeHandles, Marshaling                                 │
└────────────────────────┬────────────────────────────────────┘
                         │ P/Invoke
┌────────────────────────▼────────────────────────────────────┐
│      Databento.Native (C Wrapper - CMake)                   │
│   • C exports wrapping databento-cpp                        │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│            databento-cpp (Git Submodule)                    │
│   • Live streaming, Historical queries                      │
│   • DBN encoding/decoding                                   │
└─────────────────────────────────────────────────────────────┘
```

## Prerequisites

### For Building

**Required:**
- .NET 8 SDK or later
- CMake 3.24 or later
- C++17 compatible compiler:
  - Windows: Visual Studio 2019 or later
  - Linux: GCC 9+ or Clang 10+
  - macOS: Xcode 11+

**Automatically fetched by CMake:**
- databento-cpp (via FetchContent)
- OpenSSL 3.0+
- Zstandard (zstd)
- nlohmann_json

### For Using (NuGet Package)

- .NET 8 Runtime or later

## Quick Start

### Installation

**Windows (PowerShell):**

```powershell
# 1. Clone the repository
git clone https://github.com/Alparse/databento-dotnet.git
cd databento-dotnet

# 2. Build the native C++ library (from repository root)
.\build\build-native.ps1 -Configuration Release

# 3. Build the .NET solution (from repository root)
dotnet build databento-dotnet.sln -c Release
```

**Linux/macOS (Terminal):**

```bash
# 1. Clone the repository
git clone https://github.com/Alparse/databento-dotnet.git
cd databento-dotnet

# 2. Build the native C++ library (from repository root)
./build/build-native.sh --configuration Release

# 3. Build the .NET solution (from repository root)
dotnet build databento-dotnet.sln -c Release
```

**Note**: All build commands should be run from the repository root directory (`databento-dotnet/`).

### Live Streaming Example

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

// Get API key from environment variable (secure)
var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
    ?? throw new InvalidOperationException("DATABENTO_API_KEY environment variable not set");

// Create live client
await using var client = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .Build();

// Subscribe to events
client.DataReceived += (sender, e) =>
{
    Console.WriteLine($"Received: {e.Record}");
};

// Subscribe to NVDA trades
await client.SubscribeAsync(
    dataset: "EQUS.MINI",
    schema: Schema.Trades,
    symbols: new[] { "NVDA" }
);

// Start streaming
await client.StartAsync();

// Stream records using IAsyncEnumerable
await foreach (var record in client.StreamAsync())
{
    // Process records
    if (record is TradeMessage trade)
    {
        Console.WriteLine($"Trade: {trade.InstrumentId} @ {trade.PriceDecimal}");
    }
}
```

### Historical Data Example

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

// Get API key from environment variable (secure)
var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
    ?? throw new InvalidOperationException("DATABENTO_API_KEY environment variable not set");

// Create historical client
await using var client = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();

// Define time range - Static trading day: November 11-12, 2025
var startTime = new DateTimeOffset(2025, 11, 11, 0, 0, 0, TimeSpan.Zero); // 11/11/2025 00:00 UTC
var endTime = new DateTimeOffset(2025, 11, 12, 23, 59, 59, TimeSpan.Zero);   // 11/12/2025 23:59 UTC

// Query historical trades
await foreach (var record in client.GetRangeAsync(
    dataset: "EQUS.MINI",
    schema: Schema.Trades,
    symbols: new[] { "NVDA" },
    startTime: startTime,
    endTime: endTime))
{
    Console.WriteLine($"Historical record: {record}");
}
```

**Try it yourself:** Run the included example with:
```bash
dotnet run --project examples/Historical.Readme.Example/Historical.Readme.Example.csproj
```


## Building

### Build All (Native + .NET)

```bash
# Windows
./build/build-all.ps1 -Configuration Release

# Linux/macOS
./build/build-all.sh --configuration Release
```

### Build Native Library Only

```bash
# Windows
./build/build-native.ps1 -Configuration Release

# Linux/macOS
./build/build-native.sh --configuration Release
```

### Build .NET Solution Only

```bash
dotnet build databento-dotnet.sln -c Release
```

## Project Structure

```
databento_client/
├── src/
│   ├── Databento.Native/          # C++ native wrapper
│   │   ├── include/               # C API headers
│   │   ├── src/                   # C++ implementation
│   │   └── CMakeLists.txt
│   ├── Databento.Interop/         # P/Invoke layer
│   │   ├── Native/                # P/Invoke declarations
│   │   └── Handles/               # SafeHandle wrappers
│   └── Databento.Client/          # High-level .NET API
│       ├── Live/                  # Live streaming
│       ├── Historical/            # Historical queries
│       ├── Reference/             # Reference data APIs
│       ├── Models/                # Data models
│       └── Builders/              # Builder pattern
├── examples/
│   ├── LiveStreaming.Example/
│   ├── HistoricalData.Example/
│   ├── Reference.Example/
│   └── (26+ examples total)
├── build/
│   ├── build-native.ps1           # Native build (Windows)
│   ├── build-native.sh            # Native build (Linux/macOS)
│   └── build-all.ps1              # Full solution build
└── databento-dotnet.sln           # Visual Studio solution
```

## Running Examples

```bash
# Set API key
export DATABENTO_API_KEY=your-api-key  # Linux/macOS
$env:DATABENTO_API_KEY="your-api-key"  # Windows PowerShell

# Run live streaming example
dotnet run --project examples/LiveStreaming.Example

# Run historical data example
dotnet run --project examples/HistoricalData.Example

# Run reference data example
dotnet run --project examples/Reference.Example
```

## Supported Schemas

- **MBO**: Market by order
- **MBP-1**: Market by price (Level 1)
- **MBP-10**: Market by price (Level 10)
- **Trades**: Trade messages
- **OHLCV**: OHLCV bars (1s, 1m, 1h, 1d)
- **Definition**: Instrument definitions
- **Statistics**: Market statistics
- **Status**: Trading status
- **Imbalance**: Order imbalances

## API Documentation

### LiveClient

```csharp
ILiveClient client = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .Build();

// Events
client.DataReceived += (sender, e) => { /* ... */ };
client.ErrorOccurred += (sender, e) => { /* ... */ };

// Methods
await client.SubscribeAsync(dataset, schema, symbols);
await client.StartAsync();
await client.StopAsync();

// IAsyncEnumerable streaming
await foreach (var record in client.StreamAsync()) { /* ... */ }
```

### HistoricalClient

```csharp
IHistoricalClient client = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();

// Query historical data
await foreach (var record in client.GetRangeAsync(
    dataset, schema, symbols, startTime, endTime))
{
    // Process records
}
```

### ReferenceClient

```csharp
IReferenceClient client = new ReferenceClientBuilder()
    .SetApiKey(apiKey)
    .Build();

// Get latest security master data
var records = await client.SecurityMaster.GetLastAsync(
    symbols: new[] { "NVDA" },
    stypeIn: SType.RawSymbol
);

// Get security master data for a date range
var historicalRecords = await client.SecurityMaster.GetRangeAsync(
    start: DateTimeOffset.UtcNow.AddDays(-30),
    end: DateTimeOffset.UtcNow,
    symbols: new[] { "NVDA" }
);

// Get adjustment factors
var adjustments = await client.AdjustmentFactors.GetRangeAsync(
    start: DateTimeOffset.UtcNow.AddDays(-90),
    symbols: new[] { "NVDA" }
);

// Get corporate actions
var corporateActions = await client.CorporateActions.GetRangeAsync(
    start: DateTimeOffset.UtcNow.AddYears(-1),
    symbols: new[] { "NVDA" }
);
```

## Performance Considerations

1. **Memory Management**: Records are copied from native to managed memory. For high-throughput scenarios, consider batching.

2. **Threading**: Callbacks fire on native threads. The library marshals them to the .NET thread pool via `Channel<T>`.

3. **Backpressure**: The `Channel<T>` is unbounded by default. Consider adding bounds for memory-constrained environments.

4. **Disposal**: Always use `await using` to ensure proper resource cleanup.

## Troubleshooting

### Native Library Not Found

Ensure the native library is built and copied to the output directory:

```bash
# Rebuild native library
./build/build-native.ps1
```

### CMake Configuration Fails

Ensure all prerequisites are installed:

```bash
# Windows (with chocolatey)
choco install cmake visualstudio2022buildtools

# Linux (Ubuntu/Debian)
sudo apt-get install cmake build-essential libssl-dev libzstd-dev

# macOS (with Homebrew)
brew install cmake openssl zstd
```

### API Authentication Errors

Verify your API key is correct and has the required permissions:

```bash
# Test API key
curl -H "Authorization: Bearer your-api-key" https://api.databento.com/v1/metadata.list_datasets
```

## License

Apache 2.0 License. See [LICENSE](LICENSE) for details.

## Resources

- [Databento Documentation](https://docs.databento.com)
- [databento-cpp GitHub](https://github.com/databento/databento-cpp)
- [Issue Tracker](https://github.com/Alparse/databento_client/issues)

## Acknowledgments

Built on top of [Databento's official C++ client](https://github.com/databento/databento-cpp).
