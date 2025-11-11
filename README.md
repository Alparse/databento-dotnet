# Databento.NET

A high-performance .NET client for accessing [Databento](https://databento.com) market data, supporting both real-time streaming and historical data queries.

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
| Mbp10Message | ✅ | 368 bytes | Market by Price Level 10 (RType 0x02) |
| OhlcvMessage | ✅ | 56 bytes | OHLCV bars - 1s, 1m, 1h, 1d, EOD (RType 0x12-0x16) |
| StatusMessage | ✅ | 40 bytes | Trading status (RType 0x17) |
| InstrumentDefMessage | ✅ | 520 bytes | Instrument definitions (RType 0x18) |
| ImbalanceMessage | ✅ | 112 bytes | Order imbalances (RType 0x19) |
| ErrorMessage | ✅ | 320 bytes | Error messages (RType 0x1A) |
| SymbolMappingMessage | ✅ | 176 bytes | Symbol mappings (RType 0x1B) |
| SystemMessage | ✅ | 320 bytes | System messages & heartbeats (RType 0x1C) |
| StatMessage | ✅ | 80 bytes | Market statistics (RType 0x1D) |
| BboMessage | ✅ | 80 bytes | Best Bid/Offer - 1s, 1m (RType 0xC2-0xC3) |
| CbboMessage | ✅ | 80 bytes | Consolidated BBO - 1s, 1m, tick (RType 0xB2-0xB4) |
| Cmbp1Message | ✅ | 80 bytes | Consolidated Market by Price (RType 0xB1) |
| UnknownRecord | ✅ | Variable | Fallback for unrecognized types |

### API Coverage

| Feature | databento-cpp | Databento.NET | Status |
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
| **Historical Client** | ✅ | ⚠️ | Partial (streaming only) |
| Time-range queries | ✅ | ⚠️ | Wrapper exists, needs testing |
| Batch downloads | ✅ | ❌ | Not yet implemented |
| **Metadata & Symbol Mapping** | ✅ | ❌ | Not yet implemented |
| Instrument metadata queries | ✅ | ❌ | Planned |
| Symbol resolution | ✅ | ❌ | Planned |
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

**Phase 3 (Current)** - Helper Classes & Utilities
- Added FlagSet for bit flag manipulation (Last, Tob, Snapshot, Mbp, BadTsRecv, MaybeBadBook, PublisherSpecific)
- Added Constants with sentinel values (UndefPrice, UndefTimestamp, FixedPriceScale)
- Fixed Schema enum to include all OHLCV variants
- All builds succeed with 0 errors

**Phase 2** - Complete Record Type Implementation
- Implemented remaining 8 record types (ImbalanceMessage, StatMessage, ErrorMessage, SystemMessage, SymbolMappingMessage, BboMessage, CbboMessage, Cmbp1Message)
- Updated Record.FromBytes with all RType mappings (0x00-0x1D, 0xA0, 0xB1-0xB4, 0xC2-0xC3)
- Added ConsolidatedBidAskPair for multi-venue data

**Phase 1** - Initial Implementation
- Live streaming client with async/await support
- TradeMessage, MboMessage, Mbp1/10Message, OhlcvMessage, StatusMessage, InstrumentDefMessage
- Tested successfully with EQUS.MINI dataset (AAPL trades)

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

```bash
# Clone the repository
git clone https://github.com/yourusername/databento-dotnet.git
cd databento-dotnet

# Build native library
./build/build-native.ps1     # Windows
./build/build-native.sh      # Linux/macOS

# Build .NET solution
dotnet build Databento.NET.sln
```

### Live Streaming Example

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

// Create live client
await using var client = new LiveClientBuilder()
    .WithApiKey("your-api-key")
    .Build();

// Subscribe to events
client.DataReceived += (sender, e) =>
{
    Console.WriteLine($"Received: {e.Record}");
};

// Subscribe to ES futures trades
await client.SubscribeAsync(
    dataset: "GLBX.MDP3",
    schema: Schema.Trades,
    symbols: new[] { "ES.FUT" }
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

// Create historical client
await using var client = new HistoricalClientBuilder()
    .WithApiKey("your-api-key")
    .Build();

// Define time range
var endTime = DateTimeOffset.UtcNow;
var startTime = endTime.AddDays(-1);

// Query historical trades
await foreach (var record in client.GetRangeAsync(
    dataset: "GLBX.MDP3",
    schema: Schema.Trades,
    symbols: new[] { "ES.FUT" },
    startTime: startTime,
    endTime: endTime))
{
    Console.WriteLine($"Historical record: {record}");
}
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
dotnet build Databento.NET.sln -c Release
```

## Project Structure

```
Databento.NET/
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
│       ├── Models/                # Data models
│       └── Builders/              # Builder pattern
├── tests/
│   ├── Databento.Client.Tests/
│   └── Databento.Interop.Tests/
├── examples/
│   ├── LiveStreaming.Example/
│   └── HistoricalData.Example/
├── build/
│   ├── build-native.ps1           # Native build (Windows)
│   ├── build-native.sh            # Native build (Linux/macOS)
│   └── build-all.ps1              # Full solution build
└── Databento.NET.sln              # Visual Studio solution
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
```

## Testing

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Databento.Client.Tests
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

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests
5. Submit a pull request

## License

Apache 2.0 License. See [LICENSE](LICENSE) for details.

## Resources

- [Databento Documentation](https://docs.databento.com)
- [databento-cpp GitHub](https://github.com/databento/databento-cpp)
- [Issue Tracker](https://github.com/yourusername/databento-dotnet/issues)

## Acknowledgments

Built on top of [Databento's official C++ client](https://github.com/databento/databento-cpp).
