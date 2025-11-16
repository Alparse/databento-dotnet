# NuGet Publishing Guide - databento-dotnet

This guide walks you through publishing the `Databento.Client` NuGet package to NuGet.org.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Prepare for Publishing](#prepare-for-publishing)
3. [Build and Pack](#build-and-pack)
4. [Publish to NuGet.org](#publish-to-nugetorg)
5. [User Installation](#user-installation)
6. [User Usage](#user-usage)
7. [Updating the Package](#updating-the-package)

---

## Prerequisites

### 1. NuGet Account

Create a free account at [NuGet.org](https://www.nuget.org/)

### 2. API Key

1. Go to https://www.nuget.org/account/apikeys
2. Click **"Create"**
3. Set:
   - **Key Name**: `databento-dotnet-publish`
   - **Select Scopes**: `Push` and `Push new packages and package versions`
   - **Glob Pattern**: `Databento.*`
   - **Expiration**: Choose appropriate duration
4. Click **"Create"**
5. **Copy the API key immediately** (you won't see it again)

### 3. Store API Key Locally

**Windows (PowerShell):**
```powershell
# Store in environment variable (session only)
$env:NUGET_API_KEY="your-api-key-here"

# Or configure NuGet globally (recommended)
dotnet nuget setApiKey your-api-key-here -Source https://api.nuget.org/v3/index.json
```

**Linux/macOS:**
```bash
# Store in environment variable (session only)
export NUGET_API_KEY="your-api-key-here"

# Or configure NuGet globally (recommended)
dotnet nuget setApiKey your-api-key-here -Source https://api.nuget.org/v3/index.json
```

---

## Prepare for Publishing

### 1. Verify Native Libraries Are in Place

Ensure native libraries are correctly organized:

```
src/Databento.Interop/runtimes/
├── win-x64/
│   └── native/
│       ├── databento_native.dll
│       ├── libcrypto-3-x64.dll
│       ├── libssl-3-x64.dll
│       ├── zstd.dll
│       └── zlib1.dll
├── linux-x64/           # Add when available
│   └── native/
│       ├── libdatabento_native.so
│       └── ... (other .so files)
└── osx-x64/             # Add when available
    └── native/
        ├── libdatabento_native.dylib
        └── ... (other .dylib files)
```

**To copy Windows binaries to the runtimes folder:**

```powershell
# Windows (PowerShell) - from repository root
Copy-Item "build\native\Release\*.dll" -Destination "src\Databento.Interop\runtimes\win-x64\native\" -Force
```

### 2. Update Version Numbers (Already Done)

The projects are configured with version `3.0.5-beta`. To update for future releases:

**src/Databento.Client/Databento.Client.csproj:**
```xml
<Version>3.0.5-beta</Version>
<PackageReleaseNotes>v3.0.5-beta: Description of changes</PackageReleaseNotes>
```

**src/Databento.Interop/Databento.Interop.csproj:**
```xml
<Version>3.0.5-beta</Version>
<PackageReleaseNotes>v3.0.5-beta: Description of changes</PackageReleaseNotes>
```

### 3. Clean Previous Builds

```bash
dotnet clean databento-dotnet.sln -c Release
```

---

## Build and Pack

### Option 1: Build and Pack in One Step

```bash
# From repository root
dotnet pack src/Databento.Client/Databento.Client.csproj -c Release
```

This creates:
- `src/Databento.Client/bin/Release/Databento.Client.3.0.5-beta.nupkg`
- `src/Databento.Interop/bin/Release/Databento.Interop.3.0.5-beta.nupkg` (via dependency)

### Option 2: Build, Then Pack

```bash
# 1. Build in Release mode
dotnet build databento-dotnet.sln -c Release

# 2. Pack the main client (includes Interop automatically)
dotnet pack src/Databento.Client/Databento.Client.csproj -c Release --no-build
```

### Verify Package Contents

Before publishing, inspect the package:

```bash
# Extract and view contents (requires 7-Zip or similar)
# On Windows:
7z x src/Databento.Client/bin/Release/Databento.Client.3.0.5-beta.nupkg -o"temp_package"
dir temp_package /s

# Or use NuGet Package Explorer (GUI tool)
# Download from: https://github.com/NuGetPackageExplorer/NuGetPackageExplorer
```

**Expected structure:**
```
Databento.Client.3.0.5-beta.nupkg
├── lib/
│   └── net8.0/
│       ├── Databento.Client.dll
│       ├── Databento.Client.xml (documentation)
│       └── Databento.Interop.dll
├── runtimes/
│   └── win-x64/
│       └── native/
│           ├── databento_native.dll
│           ├── libcrypto-3-x64.dll
│           └── ... (other DLLs)
├── README.md
├── [Content_Types].xml
└── Databento.Client.nuspec
```

---

## Publish to NuGet.org

### 1. Test with Local Feed First (Optional but Recommended)

Create a local NuGet feed for testing:

```bash
# Create local feed directory
mkdir C:\LocalNuGet  # Windows
# or
mkdir ~/LocalNuGet   # Linux/macOS

# Copy package to local feed
cp src/Databento.Client/bin/Release/Databento.Client.3.0.5-beta.nupkg C:\LocalNuGet\

# Add local source
dotnet nuget add source C:\LocalNuGet -n "Local Feed"

# Test installation in a new project
mkdir test-project && cd test-project
dotnet new console
dotnet add package Databento.Client -v 3.0.5-beta -s "Local Feed"
dotnet run
```

### 2. Publish to NuGet.org

**Using API Key from Environment:**
```bash
dotnet nuget push src/Databento.Client/bin/Release/Databento.Client.3.0.5-beta.nupkg \
    --api-key $env:NUGET_API_KEY \
    --source https://api.nuget.org/v3/index.json
```

**Using Configured API Key:**
```bash
dotnet nuget push src/Databento.Client/bin/Release/Databento.Client.3.0.5-beta.nupkg \
    --source https://api.nuget.org/v3/index.json
```

**Expected output:**
```
Pushing Databento.Client.3.0.5-beta.nupkg to 'https://www.nuget.org/api/v2/package'...
  PUT https://www.nuget.org/api/v2/package/
  Created https://www.nuget.org/api/v2/package/ 2145ms
Your package was pushed.
```

### 3. Wait for Package Indexing

- **Validation**: ~5 minutes
- **Searchable on NuGet.org**: ~15-30 minutes
- You'll receive an email when the package is published

### 4. Verify on NuGet.org

Visit: https://www.nuget.org/packages/Databento.Client/

---

## User Installation

Once published, users can install your package in several ways:

### Method 1: .NET CLI

```bash
dotnet add package Databento.Client --version 3.0.5-beta
```

Or for stable releases (when available):
```bash
dotnet add package Databento.Client
```

### Method 2: Visual Studio Package Manager

1. Right-click on project → **Manage NuGet Packages**
2. Search for **"Databento.Client"**
3. Check **"Include prerelease"** (for beta versions)
4. Click **Install**

### Method 3: Package Manager Console (Visual Studio)

```powershell
Install-Package Databento.Client -Version 3.0.5-beta
```

### Method 4: Direct .csproj Reference

Add to your `.csproj` file:

```xml
<ItemGroup>
  <PackageReference Include="Databento.Client" Version="3.0.5-beta" />
</ItemGroup>
```

Then run:
```bash
dotnet restore
```

---

## User Usage

### 1. Set Environment Variable

**Windows (PowerShell):**
```powershell
$env:DATABENTO_API_KEY="db-your-api-key-here"
```

**Linux/macOS:**
```bash
export DATABENTO_API_KEY="db-your-api-key-here"
```

**Permanent (Windows System Environment):**
1. Search for "Environment Variables" in Windows
2. Add new system variable:
   - Name: `DATABENTO_API_KEY`
   - Value: `db-your-api-key-here`

### 2. Create a New Project

```bash
mkdir my-trading-app
cd my-trading-app
dotnet new console
dotnet add package Databento.Client --version 3.0.5-beta
```

### 3. Write Code

**Program.cs:**
```csharp
using Databento.Client.Builders;
using Databento.Client.Models;

// Get API key from environment variable
var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
    ?? throw new InvalidOperationException("DATABENTO_API_KEY not set");

// Live Streaming Example
await using var liveClient = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .Build();

liveClient.DataReceived += (sender, e) =>
{
    if (e.Record is TradeMessage trade)
    {
        Console.WriteLine($"Trade: {trade.InstrumentId} @ {trade.PriceDecimal} x {trade.Size}");
    }
};

await liveClient.SubscribeAsync(
    dataset: "EQUS.MINI",
    schema: Schema.Trades,
    symbols: new[] { "NVDA" }
);

await liveClient.StartAsync();
await liveClient.BlockUntilStoppedAsync(TimeSpan.FromMinutes(1));

// Historical Data Example
await using var historicalClient = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();

var start = DateTimeOffset.UtcNow.AddDays(-1);
var end = DateTimeOffset.UtcNow;

await foreach (var record in historicalClient.GetRangeAsync(
    dataset: "EQUS.MINI",
    schema: Schema.Trades,
    symbols: new[] { "NVDA" },
    startTime: start,
    endTime: end))
{
    if (record is TradeMessage trade)
    {
        Console.WriteLine($"Historical: {trade.Timestamp} - {trade.PriceDecimal}");
    }
}
```

### 4. Run

```bash
dotnet run
```

### 5. Output

```
Trade: 123456 @ 145.23 x 100
Trade: 123456 @ 145.24 x 250
Trade: 123456 @ 145.23 x 150
...
Historical: 2025-11-15T10:30:00Z - 144.50
Historical: 2025-11-15T10:30:01Z - 144.52
...
```

---

## Updating the Package

### For Future Releases

1. **Update Version:**
   ```xml
   <!-- In both .csproj files -->
   <Version>3.0.6-beta</Version>  <!-- or 3.1.0 for stable -->
   <PackageReleaseNotes>v3.0.6-beta: Bug fixes and improvements</PackageReleaseNotes>
   ```

2. **Tag Release in Git:**
   ```bash
   git tag v3.0.6-beta
   git push origin v3.0.6-beta
   ```

3. **Build and Pack:**
   ```bash
   dotnet clean -c Release
   dotnet pack src/Databento.Client/Databento.Client.csproj -c Release
   ```

4. **Publish:**
   ```bash
   dotnet nuget push src/Databento.Client/bin/Release/Databento.Client.3.0.6-beta.nupkg \
       --source https://api.nuget.org/v3/index.json
   ```

5. **Update GitHub Release:**
   ```bash
   gh release create v3.0.6-beta \
       --title "v3.0.6-beta" \
       --notes "Release notes here" \
       --prerelease
   ```

### Versioning Guidelines

- **Prerelease**: `3.0.5-beta`, `3.1.0-rc.1`
- **Stable**: `3.1.0`, `3.1.1`
- Follow [Semantic Versioning](https://semver.org/):
  - **Major** (3.x.x): Breaking changes
  - **Minor** (x.1.x): New features, backward compatible
  - **Patch** (x.x.1): Bug fixes, backward compatible

---

## Package Statistics & Monitoring

### NuGet.org Dashboard

Monitor your package at:
- https://www.nuget.org/packages/Databento.Client/
- View download statistics
- See dependent packages
- Monitor user feedback

### Package Badge

Add to README.md:

```markdown
[![NuGet](https://img.shields.io/nuget/v/Databento.Client.svg)](https://www.nuget.org/packages/Databento.Client/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Databento.Client.svg)](https://www.nuget.org/packages/Databento.Client/)
```

---

## Troubleshooting

### Issue: "Package already exists"

If you try to push the same version twice:
```
error: Response status code does not indicate success: 409 (Conflict - The feed already contains 'Databento.Client 3.0.5-beta'.)
```

**Solution:** Increment the version number.

### Issue: Native DLLs Not Included

If users report missing DLLs:

1. Verify `runtimes/` folder structure
2. Ensure `.csproj` includes:
   ```xml
   <None Include="runtimes\**\*" Pack="true" PackagePath="runtimes" />
   ```
3. Re-pack and verify with NuGet Package Explorer

### Issue: Users Can't Find Package

- Wait 15-30 minutes for indexing
- Check if package is listed (not unlisted)
- Verify users have "Include prerelease" checked for beta versions

### Issue: API Key Issues

```bash
# Verify API key is configured
dotnet nuget list source

# Re-add if needed
dotnet nuget remove source nuget.org
dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
dotnet nuget setApiKey your-api-key-here -Source https://api.nuget.org/v3/index.json
```

---

## Additional Resources

- **NuGet Documentation**: https://learn.microsoft.com/en-us/nuget/
- **Package Explorer Tool**: https://github.com/NuGetPackageExplorer/NuGetPackageExplorer
- **Semantic Versioning**: https://semver.org/
- **NuGet Package Signing**: https://learn.microsoft.com/en-us/nuget/create-packages/sign-a-package

---

## Checklist Before Publishing

- [ ] Version numbers updated in both .csproj files
- [ ] Release notes updated
- [ ] Native libraries in correct `runtimes/` folders
- [ ] README.md included in package
- [ ] Documentation XML generated
- [ ] Clean build completed
- [ ] Package tested locally
- [ ] Git committed and tagged
- [ ] GitHub release created
- [ ] NuGet API key configured
- [ ] Published to NuGet.org
- [ ] Verified on NuGet.org after 30 minutes

---

**Happy Publishing! 🚀**
