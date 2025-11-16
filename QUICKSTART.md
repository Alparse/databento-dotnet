# Quick Start Guide

Get up and running with databento-dotnet in minutes!

## 1. Build the Project

### Windows

```powershell
# Build everything in one command
.\build\build-all.ps1 -Configuration Release
```

### Linux/macOS

```bash
# Make build scripts executable
chmod +x build/*.sh

# Build everything
./build/build-all.sh --configuration Release
```

## 2. Set Your API Key

```bash
# Linux/macOS
export DATABENTO_API_KEY="your-api-key-here"

# Windows PowerShell
$env:DATABENTO_API_KEY = "your-api-key-here"

# Windows Command Prompt
set DATABENTO_API_KEY=your-api-key-here
```

Get your API key from: https://databento.com/portal/keys

## 3. Run an Example

### Live Streaming

```bash
dotnet run --project examples/LiveStreaming.Example
```

This will:
- Connect to Databento's live data feed
- Subscribe to ES.FUT (E-mini S&P 500) trades
- Stream 100 records
- Display them in real-time

### Historical Data

```bash
dotnet run --project examples/HistoricalData.Example
```

This will:
- Query the last 24 hours of ES.FUT trades
- Display the first 10 records
- Show total record count

## 4. Create Your Own Application

### Create a New Console App

```bash
dotnet new console -n MyDatabento App
cd MyDatabentoApp
dotnet add reference ../src/Databento.Client/Databento.Client.csproj
```

### Add Code

Edit `Program.cs`:

```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")!;

await using var client = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .Build();

client.DataReceived += (_, e) => Console.WriteLine(e.Record);

await client.SubscribeAsync("GLBX.MDP3", Schema.Trades, new[] { "ES.FUT" });
await client.StartAsync();

await foreach (var record in client.StreamAsync())
{
    // Your custom processing here
    Console.WriteLine($"Processing: {record}");
}
```

### Run It

```bash
dotnet run
```

## Common Tasks

### Subscribe to Multiple Symbols

```csharp
await client.SubscribeAsync(
    dataset: "GLBX.MDP3",
    schema: Schema.Trades,
    symbols: new[] { "ES.FUT", "NQ.FUT", "YM.FUT" }
);
```

### Query Historical Data for a Specific Date

```csharp
var date = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero);

await foreach (var record in client.GetRangeAsync(
    dataset: "GLBX.MDP3",
    schema: Schema.Trades,
    symbols: new[] { "ES.FUT" },
    startTime: date,
    endTime: date.AddDays(1)))
{
    // Process records
}
```

### Handle Different Record Types

```csharp
await foreach (var record in client.StreamAsync())
{
    switch (record)
    {
        case TradeMessage trade:
            Console.WriteLine($"Trade: {trade.InstrumentId} @ {trade.PriceDecimal} x {trade.Size}");
            break;

        case UnknownRecord unknown:
            Console.WriteLine($"Unknown record type: {unknown.RType}");
            break;
    }
}
```

### Use Events Instead of IAsyncEnumerable

```csharp
client.DataReceived += (sender, e) =>
{
    if (e.Record is TradeMessage trade)
    {
        Console.WriteLine($"Trade: {trade.PriceDecimal}");
    }
};

client.ErrorOccurred += (sender, e) =>
{
    Console.WriteLine($"Error: {e.Exception.Message}");
};

await client.StartAsync();

// Keep running until Ctrl+C
await Task.Delay(Timeout.Infinite);
```

## Supported Datasets

Common datasets:
- `GLBX.MDP3` - CME Globex MDP 3.0
- `XNAS.ITCH` - Nasdaq TotalView-ITCH
- `XNYS.PILLAR` - NYSE Pillar
- `DBEQ.BASIC` - Databento Equities Basic

See full list: https://docs.databento.com/knowledge-base/datasets

## Supported Schemas

- `Schema.Mbo` - Market by order
- `Schema.Mbp1` - Level 1 market by price
- `Schema.Mbp10` - Level 10 market by price
- `Schema.Trades` - Trade messages
- `Schema.Ohlcv1S` - 1-second bars
- `Schema.Ohlcv1M` - 1-minute bars
- `Schema.Ohlcv1H` - 1-hour bars
- `Schema.Ohlcv1D` - 1-day bars
- `Schema.Definition` - Instrument definitions
- `Schema.Statistics` - Market statistics
- `Schema.Status` - Trading status
- `Schema.Imbalance` - Order imbalances

## Troubleshooting

### "databento_native.dll not found"

The native library wasn't built or copied. Run:

```bash
.\build\build-native.ps1  # Windows
./build/build-native.sh   # Linux/macOS
```

### "Authentication failed"

Check your API key:

```bash
echo $DATABENTO_API_KEY  # Linux/macOS
echo %DATABENTO_API_KEY% # Windows CMD
$env:DATABENTO_API_KEY   # Windows PowerShell
```

### "No data received"

1. Check if markets are open
2. Verify symbol format (e.g., `ES.FUT` not `ESH4`)
3. Check dataset subscription on Databento portal

### Build errors

See [BUILDING.md](BUILDING.md) for detailed troubleshooting.

## Next Steps

- Read [README.md](README.md) for full documentation
- Explore [examples/](examples/) for more use cases
- Check [Databento documentation](https://docs.databento.com)
- Review the [API reference](https://docs.databento.com/api-reference-historical)

## Getting Help

- GitHub Issues: https://github.com/Alparse/databento_client/issues
- Databento Support: https://databento.com/support
- Documentation: https://docs.databento.com

## License

Apache 2.0 - See [LICENSE](LICENSE) for details.
