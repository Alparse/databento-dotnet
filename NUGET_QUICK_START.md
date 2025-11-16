# NuGet Quick Start - databento-dotnet

## Publishing to NuGet (One-Time Setup)

### 1. Get NuGet API Key
```bash
# Visit https://www.nuget.org/account/apikeys
# Create new key with Push scope
# Copy and store:
dotnet nuget setApiKey YOUR_API_KEY -Source https://api.nuget.org/v3/index.json
```

### 2. Build & Publish
```bash
# From repository root
dotnet pack src/Databento.Client/Databento.Client.csproj -c Release
dotnet nuget push src/Databento.Client/bin/Release/Databento.Client.3.0.6-beta.nupkg --source https://api.nuget.org/v3/index.json
```

**Note:** As of v3.0.6-beta, all native binaries and the Interop layer are embedded in the single `Databento.Client` package. No separate `Databento.Interop` package is needed.

### 3. Wait & Verify
- Wait 15-30 minutes for indexing
- Check: https://www.nuget.org/packages/Databento.Client/

---

## User Installation & Usage

### Install Package
```bash
dotnet new console -n MyTradingApp
cd MyTradingApp
dotnet add package Databento.Client --version 3.0.6-beta
```

### Set API Key
```bash
# Windows
$env:DATABENTO_API_KEY="db-your-key-here"

# Linux/macOS
export DATABENTO_API_KEY="db-your-key-here"
```

### Write Code (Program.cs)
```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")!;

// Live Streaming
await using var client = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .Build();

client.DataReceived += (s, e) =>
{
    if (e.Record is TradeMessage t)
        Console.WriteLine($"Trade: {t.PriceDecimal} x {t.Size}");
};

await client.SubscribeAsync("EQUS.MINI", Schema.Trades, new[] { "NVDA" });
await client.StartAsync();
await client.BlockUntilStoppedAsync(TimeSpan.FromMinutes(1));
```

### Run
```bash
dotnet run
```

---

## Future Updates

```bash
# 1. Update version in .csproj files (both Client and Interop)
# 2. Build & pack
dotnet pack src/Databento.Client/Databento.Client.csproj -c Release

# 3. Push
dotnet nuget push src/Databento.Client/bin/Release/Databento.Client.3.0.7-beta.nupkg --source https://api.nuget.org/v3/index.json

# 4. Tag in git
git tag v3.0.7-beta
git push origin v3.0.7-beta
```

---

**For full documentation, see [NUGET_PUBLISHING_GUIDE.md](NUGET_PUBLISHING_GUIDE.md)**
