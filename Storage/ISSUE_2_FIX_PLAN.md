# Issue #2 Fix Plan - Comprehensive Solution

**Date**: November 22, 2025
**Status**: Ready for Implementation
**Priority**: HIGH

---

## Executive Summary

Issue #2 (DllNotFoundException) was "fixed" in v3.0.23-beta by bundling VC++ runtime DLLs. However, a user TODAY still experienced the issue. Investigation reveals the fix IS working, but users may not be benefiting due to:

1. Using old versions (< 3.0.23-beta)
2. Stale NuGet cache
3. Platform incompatibility (Linux/Mac not supported)
4. Build configuration issues

This plan addresses all failure modes with immediate and long-term solutions.

---

## Phase 1: Immediate Actions (TODAY) 🔴

### Action 1.1: Contact the User

**Goal**: Identify specific failure mode

**Questions to Ask**:
```
1. What version are you using?
   Command: dotnet list package | grep Databento

2. What operating system?
   Windows / Linux / macOS

3. What's in your output directory?
   Command: ls bin/Debug/net8.0/*.dll | grep -E "(databento|msvcp|vcruntime)"

4. How did you install the package?
   Copy exact command used

5. Did you upgrade from an older version?
   Yes / No - if yes, from which version?
```

**Expected Outcomes**:
- **If version < 3.0.23-beta**: Guide through upgrade (Action 1.2)
- **If version >= 3.0.23-beta but DLLs missing**: Cache issue (Action 1.3)
- **If Linux/Mac**: Platform not supported (Action 1.4)

---

### Action 1.2: User Upgrade Guide

**If user is on version < 3.0.23-beta**:

```bash
# Step 1: Update to latest version
dotnet add package Databento.Client --version 3.0.24-beta --prerelease

# Step 2: Clear NuGet cache (important!)
dotnet nuget locals all --clear

# Step 3: Clean and rebuild
dotnet clean
dotnet restore --force
dotnet build

# Step 4: Verify DLLs are present
ls bin/Debug/net8.0/ | grep -E "(msvcp|vcruntime)"
# Should show:
#   msvcp140.dll
#   vcruntime140.dll
#   vcruntime140_1.dll

# Step 5: Test
dotnet run
```

**Success Criteria**:
✅ Application runs without DllNotFoundException
✅ VC++ runtime DLLs visible in output directory
✅ No manual C++ installation needed

---

### Action 1.3: Cache Clearing Guide

**If user is on version >= 3.0.23-beta but still has issues**:

```bash
# Nuclear option - clear everything
dotnet nuget locals all --clear

# Remove bin/obj folders
rm -rf bin obj

# Force restore
dotnet restore --force --no-cache

# Clean build
dotnet clean
dotnet build --no-restore

# Verify package contents
cd ~/.nuget/packages/databento.client/3.0.24-beta/runtimes/win-x64/native/
ls -la
# Should show msvcp140.dll, vcruntime140.dll, vcruntime140_1.dll

# If DLLs present in cache but not in output, build targets issue
# Check: Are you using SDK-style project?
cat [project].csproj | head -1
# Should show: <Project Sdk="Microsoft.NET.Sdk">
```

---

### Action 1.4: Platform Not Supported Response

**If user is on Linux or macOS**:

```markdown
## Linux/macOS Not Yet Supported

The databento-dotnet package currently only supports Windows x64.

**Current Status**:
- ✅ Windows x64: Fully supported (v3.0.23-beta+)
- ❌ Linux x64: Native library not built
- ❌ macOS Intel: Native library not built
- ❌ macOS ARM: Native library not built

**Why**:
The package depends on a native C++ library (databento_native.dll/.so/.dylib)
that must be built separately for each platform. Only Windows builds
exist currently.

**Workarounds**:
None at this time. Windows is required.

**Timeline**:
Cross-platform support is planned but not yet scheduled. If you need
Linux/Mac support urgently, please:
1. Comment on GitHub issue requesting it
2. Indicate your use case and urgency
3. Consider sponsoring development if time-critical

**Why VC++ Runtime is Windows-Only Issue**:
- Windows: C++ runtime NOT included with OS → must bundle
- Linux: libstdc++ IS included with OS → no bundling needed
- macOS: libc++ IS included with OS → no bundling needed

The VC++ DLL issue only affects Windows, but the package itself
needs platform-specific native libraries to work anywhere.
```

---

## Phase 2: Documentation Updates (THIS WEEK) 🟡

### Action 2.1: Update README.md

**Add immediately after title**:

```markdown
# databento-dotnet

⚠️ **IMPORTANT VERSION REQUIREMENT**: To avoid `DllNotFoundException`, you MUST use version **3.0.23-beta or later**. See [Upgrading](#upgrading) below.

📦 **Windows Only**: Currently only supports Windows x64. Linux/macOS support is planned.

[rest of README...]
```

**Add new section before Installation**:

```markdown
## Platform Support

| Platform | Status | Required Runtime | Notes |
|----------|--------|------------------|-------|
| Windows x64 | ✅ **Supported** | Bundled (v3.0.23+) | Fully functional |
| Linux x64 | ❌ Not Available | N/A | Native library not built |
| macOS Intel | ❌ Not Available | N/A | Native library not built |
| macOS ARM | ❌ Not Available | N/A | Native library not built |

**Windows Users**: If using version < 3.0.23-beta, you must manually install
[Visual C++ Redistributable 2015-2022](https://aka.ms/vs/17/release/vc_redist.x64.exe).
Versions 3.0.23-beta and later include the required DLLs automatically.
```

**Update Installation section**:

```markdown
## Installation

### Prerequisites

**All Platforms**:
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) or later

**Windows** (v3.0.23-beta+):
- No additional prerequisites! VC++ runtime DLLs are bundled.

**Windows** (< v3.0.23-beta):
- [Visual C++ Redistributable 2015-2022](https://aka.ms/vs/17/release/vc_redist.x64.exe)

### Install Latest Version

```bash
# Recommended: Use latest beta version
dotnet add package Databento.Client --version 3.0.24-beta --prerelease
```

### Upgrading from Older Versions

If you're experiencing `DllNotFoundException` errors:

```bash
# 1. Update to latest
dotnet add package Databento.Client --version 3.0.24-beta --prerelease

# 2. Clear cache
dotnet nuget locals all --clear

# 3. Rebuild
dotnet clean && dotnet restore --force && dotnet build
```

See [UPGRADING.md](UPGRADING.md) for detailed migration guide.
```

---

### Action 2.2: Create UPGRADING.md

**File**: `UPGRADING.md`

```markdown
# Upgrading Databento.Client

## Upgrading from v3.0.22-beta or Earlier to v3.0.23-beta+

### Background

Versions **prior to 3.0.23-beta** require manual installation of Visual C++ Redistributable.
Versions **3.0.23-beta and later** bundle the required runtime DLLs automatically.

If you're experiencing `DllNotFoundException`, you're likely on an old version.

---

### Step-by-Step Upgrade

#### 1. Check Your Current Version

```bash
dotnet list package | grep Databento
```

**Output Example**:
```
Databento.Client    3.0.15-beta
```

If version shows **< 3.0.23-beta**, continue with upgrade steps below.

#### 2. Update Package

```bash
dotnet add package Databento.Client --version 3.0.24-beta --prerelease
```

Or update your `.csproj` file:

```xml
<PackageReference Include="Databento.Client" Version="3.0.24-beta" />
```

#### 3. Clear NuGet Cache

**Important**: This ensures you get fresh package contents, not cached old version.

```bash
dotnet nuget locals all --clear
```

**Windows PowerShell**:
```powershell
dotnet nuget locals all --clear
```

#### 4. Clean and Rebuild

```bash
dotnet clean
dotnet restore --force --no-cache
dotnet build
```

#### 5. Verify DLLs Are Present

**Check your output directory**:

```bash
ls bin/Debug/net8.0/ | grep -E "(msvcp|vcruntime|databento)"
```

**Expected Output** (Windows):
```
databento_native.dll
msvcp140.dll          ← Should be present!
vcruntime140.dll      ← Should be present!
vcruntime140_1.dll    ← Should be present!
libcrypto-3-x64.dll
libssl-3-x64.dll
zstd.dll
zlib1.dll
```

If `msvcp140.dll`, `vcruntime140.dll`, and `vcruntime140_1.dll` are missing, see [Troubleshooting](#troubleshooting) below.

#### 6. Test

```bash
dotnet run
```

✅ **Success**: Application runs without `DllNotFoundException`

---

## Troubleshooting

### Issue: DLLs Still Missing After Upgrade

**Cause**: NuGet cache or build targets not executing properly.

**Solution**:

```bash
# 1. Nuclear cache clear
rm -rf ~/.nuget/packages/databento.client
rm -rf bin obj

# 2. Force restore
dotnet restore --force --no-cache

# 3. Clean build
dotnet clean
dotnet build --no-restore

# 4. Manually verify package contents
ls ~/.nuget/packages/databento.client/3.0.24-beta/runtimes/win-x64/native/

# Should contain:
#   databento_native.dll
#   msvcp140.dll
#   vcruntime140.dll
#   vcruntime140_1.dll
```

### Issue: Package Restore Fails

**Error**: `No versions of 'Databento.Client' compatible with framework are available`

**Cause**: Missing `--prerelease` flag (all versions are beta).

**Solution**:
```bash
dotnet add package Databento.Client --version 3.0.24-beta --prerelease
#                                                         ^^^^^^^^^^^^ Required!
```

### Issue: Still Getting DllNotFoundException on Latest Version

**Diagnostic Steps**:

1. **Confirm version**:
   ```bash
   dotnet list package --include-transitive | grep Databento
   ```
   Should show `3.0.24-beta` or later.

2. **Check output directory**:
   ```bash
   ls -la bin/Debug/net8.0/*.dll
   ```
   Must include `msvcp140.dll`, `vcruntime140.dll`, `vcruntime140_1.dll`.

3. **Verify package contents**:
   ```bash
   cd ~/.nuget/packages/databento.client/3.0.24-beta
   ls runtimes/win-x64/native/
   ```
   DLLs must be present here too.

4. **Check project type**:
   ```bash
   head -1 *.csproj
   ```
   Should be SDK-style: `<Project Sdk="Microsoft.NET.Sdk">`

5. **Try diagnostic helper** (if available):
   ```csharp
   Databento.Client.DiagnosticHelper.VerifyRuntimeDependencies();
   ```

If all above checks pass but still have issues, please file a GitHub issue with:
- Your OS and version
- .NET SDK version (`dotnet --version`)
- Project .csproj file
- Full error stack trace

---

## Platform-Specific Notes

### Windows
- ✅ Fully supported (v3.0.23-beta+)
- VC++ runtime DLLs bundled automatically
- No manual installation required

### Linux
- ❌ Not currently supported
- Native library not built for Linux
- Workaround: Use Windows or wait for Linux support

### macOS
- ❌ Not currently supported
- Native library not built for macOS
- Workaround: Use Windows or wait for macOS support

---

## Version History

### v3.0.24-beta (November 21, 2025)
- Critical bug fix: AccessViolationException crash
- No installation changes from v3.0.23-beta

### v3.0.23-beta (November 19, 2025) ✨ **Breaking Change**
- ✅ **Fixed**: DllNotFoundException on clean Windows installs
- ✅ Bundled VC++ runtime DLLs (msvcp140, vcruntime140, vcruntime140_1)
- ✅ No more manual Visual C++ Redistributable installation required
- ⚠️ Package size increased by ~750 KB

### v3.0.22-beta and Earlier
- ❌ Requires manual Visual C++ Redistributable installation
- ❌ DllNotFoundException on systems without VC++ runtime
- ⚠️ Upgrade strongly recommended

---

## FAQ

**Q: Why do I need to upgrade?**
A: Versions before 3.0.23-beta require users to manually install Visual C++ Redistributable. Version 3.0.23-beta+ bundles the required DLLs automatically, providing a better user experience.

**Q: Will upgrading break my code?**
A: No. The upgrade only adds DLL files. All APIs remain unchanged. Your code will work without modification.

**Q: Can I stay on an older version?**
A: Yes, but you (and your users) must ensure Visual C++ Redistributable 2015-2022 is installed on all systems.

**Q: Why is the package size larger in v3.0.23+?**
A: The package now includes three VC++ runtime DLLs (~750 KB total). This is a one-time cost that eliminates the need for manual runtime installation.

**Q: Will you support Linux/macOS?**
A: It's planned but not yet scheduled. If you need it urgently, please comment on the relevant GitHub issue.

---

**Need Help?**
- [GitHub Issues](https://github.com/Alparse/databento-dotnet/issues)
- [Documentation](https://github.com/Alparse/databento-dotnet/blob/master/README.md)
```

---

### Action 2.3: Update GitHub Issue Template

**File**: `.github/ISSUE_TEMPLATE/bug_report.md`

```markdown
---
name: Bug report
about: Report a bug in databento-dotnet
title: '[BUG] '
labels: bug
assignees: ''
---

## Before Reporting

### For DllNotFoundException Errors ⚠️

Please try these steps FIRST before filing an issue:

1. **Check your version**:
   ```bash
   dotnet list package | grep Databento
   ```
   - If **< 3.0.23-beta**: Please upgrade to latest version
   - Upgrade command: `dotnet add package Databento.Client --version 3.0.24-beta --prerelease`

2. **Clear NuGet cache and rebuild**:
   ```bash
   dotnet nuget locals all --clear
   dotnet clean && dotnet restore --force && dotnet build
   ```

3. **Verify DLLs in output**:
   ```bash
   ls bin/Debug/net8.0/ | grep -E "(msvcp|vcruntime)"
   ```
   Should show: `msvcp140.dll`, `vcruntime140.dll`, `vcruntime140_1.dll`

See [UPGRADING.md](https://github.com/Alparse/databento-dotnet/blob/master/UPGRADING.md) for full migration guide.

If issue persists after above steps, please continue with bug report below.

---

## Bug Description

**Clear description of the issue**:
[Describe what's wrong]

## System Information

**Required**:
- OS: [Windows 10/11, Linux distro, macOS version]
- OS Architecture: [x64, ARM64]
- .NET SDK Version: [Output of `dotnet --version`]
- Package Version: [Output of `dotnet list package | grep Databento`]

## Steps to Reproduce

1. [First step]
2. [Second step]
3. [...]

**Minimal code example**:
```csharp
// Paste minimal code that reproduces the issue
```

## Expected Behavior

[What should happen]

## Actual Behavior

[What actually happens]

**Error Message** (if applicable):
```
[Paste full error message and stack trace]
```

## Additional Context

**Output Directory Contents**:
```bash
ls bin/Debug/net8.0/*.dll
[Paste output]
```

**NuGet Package Contents**:
```bash
ls ~/.nuget/packages/databento.client/[version]/runtimes/win-x64/native/
[Paste output]
```

**Project File** (.csproj):
```xml
[Paste relevant parts of your .csproj file]
```

[Any other relevant information]
```

---

## Phase 3: Technical Improvements (NEXT WEEK) 🟢

### Action 3.1: Add Diagnostic Helper

**File**: `src/Databento.Client/DiagnosticHelper.cs`

```csharp
using System.Reflection;
using System.Runtime.InteropServices;

namespace Databento.Client;

/// <summary>
/// Diagnostic utilities for troubleshooting installation and runtime issues.
/// </summary>
public static class DiagnosticHelper
{
    /// <summary>
    /// Verifies that all required runtime dependencies are present in the output directory.
    /// Prints diagnostic information to console.
    /// </summary>
    /// <returns>True if all dependencies found, false if any are missing.</returns>
    public static bool VerifyRuntimeDependencies()
    {
        Console.WriteLine("=== Databento.Client Runtime Dependency Check ===\n");

        // Get assembly version
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        Console.WriteLine($"Package Version: {version}");
        Console.WriteLine($"Platform: {RuntimeInformation.OSDescription}");
        Console.WriteLine($"Architecture: {RuntimeInformation.OSArchitecture}");
        Console.WriteLine($"Framework: {RuntimeInformation.FrameworkDescription}\n");

        // Base directory where DLLs should be
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        Console.WriteLine($"Output Directory: {baseDir}\n");

        // Platform-specific required DLLs
        string[] requiredDlls;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            requiredDlls = new[]
            {
                "databento_native.dll",
                "msvcp140.dll",           // VC++ C++ Standard Library
                "vcruntime140.dll",       // VC++ Runtime Core
                "vcruntime140_1.dll",     // VC++ Runtime Extended
                "libcrypto-3-x64.dll",    // OpenSSL Crypto
                "libssl-3-x64.dll",       // OpenSSL SSL
                "zstd.dll",               // Zstandard compression
                "zlib1.dll",              // Zlib compression
                "legacy.dll"              // OpenSSL legacy algorithms
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            requiredDlls = new[]
            {
                "libdatabento_native.so"
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            requiredDlls = new[]
            {
                "libdatabento_native.dylib"
            };
        }
        else
        {
            Console.WriteLine("❌ Unsupported platform!");
            return false;
        }

        var missingDlls = new List<(string Name, string Path)>();
        var foundDlls = new List<(string Name, string Path, long Size)>();

        foreach (var dll in requiredDlls)
        {
            var path = Path.Combine(baseDir, dll);
            if (File.Exists(path))
            {
                var size = new FileInfo(path).Length;
                foundDlls.Add((dll, path, size));
            }
            else
            {
                missingDlls.Add((dll, path));
            }
        }

        // Print found DLLs
        if (foundDlls.Any())
        {
            Console.WriteLine("✅ Found Dependencies:");
            foreach (var (name, path, size) in foundDlls)
            {
                var sizeKB = size / 1024.0;
                Console.WriteLine($"   {name,-25} ({sizeKB,7:N0} KB)");
            }
            Console.WriteLine();
        }

        // Print missing DLLs
        if (missingDlls.Any())
        {
            Console.WriteLine("❌ Missing Dependencies:");
            foreach (var (name, path) in missingDlls)
            {
                Console.WriteLine($"   {name}");
            }
            Console.WriteLine();

            // Provide platform-specific troubleshooting
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Check for VC++ runtime specifically
                var vcMissing = missingDlls.Any(d =>
                    d.Name.Contains("msvcp") || d.Name.Contains("vcruntime"));

                if (vcMissing)
                {
                    Console.WriteLine("⚠️ Visual C++ Runtime DLLs Missing");
                    Console.WriteLine();
                    Console.WriteLine("Possible causes:");
                    Console.WriteLine("  1. Using Databento.Client < v3.0.23-beta");
                    Console.WriteLine("     → Solution: Upgrade to v3.0.24-beta or later");
                    Console.WriteLine("     → Command: dotnet add package Databento.Client --version 3.0.24-beta --prerelease");
                    Console.WriteLine();
                    Console.WriteLine("  2. NuGet cache is stale");
                    Console.WriteLine("     → Solution: Clear cache and rebuild");
                    Console.WriteLine("     → Commands:");
                    Console.WriteLine("       dotnet nuget locals all --clear");
                    Console.WriteLine("       dotnet clean && dotnet restore --force && dotnet build");
                    Console.WriteLine();
                    Console.WriteLine("  3. Build targets didn't execute");
                    Console.WriteLine("     → Solution: Clean rebuild");
                    Console.WriteLine("     → Commands:");
                    Console.WriteLine("       dotnet clean");
                    Console.WriteLine("       dotnet build");
                    Console.WriteLine();
                }
            }
            else
            {
                Console.WriteLine($"⚠️ {RuntimeInformation.OSDescription} is not currently supported.");
                Console.WriteLine();
                Console.WriteLine("Databento.Client currently only supports Windows x64.");
                Console.WriteLine("Linux and macOS support is planned for a future release.");
                Console.WriteLine();
            }

            Console.WriteLine("For more help, see:");
            Console.WriteLine("  https://github.com/Alparse/databento-dotnet/blob/master/UPGRADING.md");
            Console.WriteLine();

            return false;
        }

        Console.WriteLine("✅ All required runtime dependencies are present!");
        Console.WriteLine();
        return true;
    }

    /// <summary>
    /// Gets the current package version.
    /// </summary>
    public static Version GetPackageVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version
            ?? new Version(0, 0, 0);
    }

    /// <summary>
    /// Checks if the current version is at least the specified minimum version.
    /// </summary>
    public static bool IsVersionAtLeast(int major, int minor, int build)
    {
        var current = GetPackageVersion();
        var minimum = new Version(major, minor, build);
        return current >= minimum;
    }
}
```

**Example Usage in User Code**:

```csharp
using Databento.Client;

// Add at start of Main():
if (!DiagnosticHelper.VerifyRuntimeDependencies())
{
    Console.WriteLine("⚠️ Runtime dependencies missing. Application may crash.");
    Console.WriteLine("Press any key to continue anyway, or Ctrl+C to exit.");
    Console.ReadKey();
}

// Rest of application...
var client = new HistoricalClientBuilder()
    .WithApiKey(apiKey)
    .Build();
```

---

### Action 3.2: Add Runtime Version Check

**File**: `src/Databento.Client/HistoricalClientBuilder.cs` (and other builders)

Add version warning to all client builders:

```csharp
public HistoricalClient Build()
{
    // Warn if using old version (< 3.0.23)
    var version = DiagnosticHelper.GetPackageVersion();
    if (version.Major == 3 && version.Minor == 0 && version.Build < 23)
    {
        Console.WriteLine($@"
⚠️ WARNING: You are using Databento.Client v{version} (pre-release)

Versions before v3.0.23-beta require manual installation of Visual C++ Runtime.
You may experience DllNotFoundException errors.

Recommendation: Upgrade to v3.0.24-beta or later:
  dotnet add package Databento.Client --version 3.0.24-beta --prerelease

For migration guide, see:
  https://github.com/Alparse/databento-dotnet/blob/master/UPGRADING.md
");
    }

    // Rest of Build() logic...
    return new HistoricalClient(...);
}
```

---

### Action 3.3: Add MSBuild Verification Target

**File**: `src/Databento.Client/Databento.Client.csproj`

Add target to verify DLLs after build:

```xml
<!-- Add after existing ItemGroups -->

<!-- Verify VC++ runtime DLLs are copied (Windows only) -->
<Target Name="VerifyVcRuntimeDLLs"
        AfterTargets="Build"
        Condition="'$(OS)' == 'Windows_NT'">

  <PropertyGroup>
    <VcDllsFound>true</VcDllsFound>
  </PropertyGroup>

  <ItemGroup>
    <RequiredVcDlls Include="$(OutDir)msvcp140.dll" />
    <RequiredVcDlls Include="$(OutDir)vcruntime140.dll" />
    <RequiredVcDlls Include="$(OutDir)vcruntime140_1.dll" />
  </ItemGroup>

  <!-- Check each required DLL -->
  <PropertyGroup>
    <VcDllsFound Condition="!Exists('$(OutDir)msvcp140.dll')">false</VcDllsFound>
    <VcDllsFound Condition="!Exists('$(OutDir)vcruntime140.dll')">false</VcDllsFound>
    <VcDllsFound Condition="!Exists('$(OutDir)vcruntime140_1.dll')">false</VcDllsFound>
  </PropertyGroup>

  <!-- Warn if any are missing -->
  <Warning
    Text="⚠️ Visual C++ Runtime DLLs not found in output directory. This may cause DllNotFoundException at runtime. Try: dotnet clean &amp;&amp; dotnet build"
    Condition="'$(VcDllsFound)' == 'false'" />

  <!-- Info message if all present -->
  <Message
    Text="✅ Visual C++ Runtime DLLs verified in output directory"
    Importance="normal"
    Condition="'$(VcDllsFound)' == 'true'" />

</Target>
```

---

## Phase 4: Testing & Validation (NEXT WEEK) 🧪

### Action 4.1: Test on Clean Windows VM

**Setup**:
1. Windows 10/11 VM with NOTHING installed except:
   - .NET 8 SDK
   - No Visual Studio
   - No Visual C++ Redistributable

**Test Case 1: Fresh Install**:
```bash
# Create new project
dotnet new console -n FreshInstallTest
cd FreshInstallTest

# Install package
dotnet add package Databento.Client --version 3.0.24-beta --prerelease

# Build
dotnet build

# Verify DLLs
ls bin/Debug/net8.0/*.dll | grep -E "(msvcp|vcruntime)"

# Expected: Should show all 3 VC++ DLLs
```

**Test Case 2: Upgrade from Old Version**:
```bash
# Create new project
dotnet new console -n UpgradeTest
cd UpgradeTest

# Install OLD version
dotnet add package Databento.Client --version 3.0.15-beta --prerelease
dotnet build

# Should fail at runtime (missing VC++)
# dotnet run  # Would fail

# Upgrade
dotnet add package Databento.Client --version 3.0.24-beta --prerelease
dotnet nuget locals all --clear
dotnet clean
dotnet restore --force
dotnet build

# Verify DLLs
ls bin/Debug/net8.0/*.dll | grep -E "(msvcp|vcruntime)"

# Expected: Should show all 3 VC++ DLLs
```

**Test Case 3: Diagnostic Helper**:
```csharp
// Program.cs
using Databento.Client;

DiagnosticHelper.VerifyRuntimeDependencies();

// Expected: Should print "✅ All required runtime dependencies are present!"
```

---

### Action 4.2: Test Package Contents

**Verify every new release**:

```bash
# Download package
dotnet nuget locals global-packages -l
# Note the path

# Or use fresh download
dotnet new console -n PackageTest
cd PackageTest
dotnet add package Databento.Client --version 3.0.24-beta --prerelease
dotnet restore

# Inspect package
cd ~/.nuget/packages/databento.client/3.0.24-beta/

# Check structure
find . -name "*.dll" -o -name "*.targets"

# Expected structure:
# ./lib/net8.0/Databento.Client.dll
# ./lib/net8.0/Databento.Interop.dll
# ./build/Databento.Client.targets
# ./runtimes/win-x64/native/databento_native.dll
# ./runtimes/win-x64/native/msvcp140.dll
# ./runtimes/win-x64/native/vcruntime140.dll
# ./runtimes/win-x64/native/vcruntime140_1.dll
# ./runtimes/win-x64/native/libcrypto-3-x64.dll
# ./runtimes/win-x64/native/libssl-3-x64.dll
# ./runtimes/win-x64/native/zstd.dll
# ./runtimes/win-x64/native/zlib1.dll
# ./runtimes/win-x64/native/legacy.dll

# All DLLs must be present!
```

---

## Phase 5: Long-Term Improvements (FUTURE) 🚀

### Action 5.1: Linux Support

**Requirements**:
1. Build `libdatabento_native.so` for Linux x64
2. Test on Ubuntu 20.04, 22.04, Debian, CentOS
3. No bundling needed (libstdc++ is system library)

**Build Commands** (on Linux machine or GitHub Actions):
```bash
cd src/Databento.Native
cmake -B build-linux -DCMAKE_BUILD_TYPE=Release
cmake --build build-linux --config Release
cp build-linux/libdatabento_native.so \
   ../Databento.Interop/runtimes/linux-x64/native/
```

**Testing**:
```bash
# On Linux machine:
dotnet add package Databento.Client --version 3.0.25-beta
dotnet build
ldd bin/Debug/net8.0/libdatabento_native.so

# Should show system libraries (libstdc++, libssl, etc.)
# NOT missing dependencies
```

---

### Action 5.2: macOS Support

**Requirements**:
1. Build `libdatabento_native.dylib` for macOS Intel (x64)
2. Build `libdatabento_native.dylib` for macOS ARM (M1/M2/M3)
3. Test on macOS 12, 13, 14
4. No bundling needed (libc++ is system library)

**Build Commands** (on Mac machine or GitHub Actions):
```bash
# Intel build
cd src/Databento.Native
cmake -B build-mac-x64 -DCMAKE_BUILD_TYPE=Release -DCMAKE_OSX_ARCHITECTURES=x86_64
cmake --build build-mac-x64
cp build-mac-x64/libdatabento_native.dylib \
   ../Databento.Interop/runtimes/osx-x64/native/

# ARM build
cmake -B build-mac-arm64 -DCMAKE_BUILD_TYPE=Release -DCMAKE_OSX_ARCHITECTURES=arm64
cmake --build build-mac-arm64
cp build-mac-arm64/libdatabento_native.dylib \
   ../Databento.Interop/runtimes/osx-arm64/native/
```

---

### Action 5.3: Automated CI Testing

**GitHub Actions Workflow**:

```yaml
# .github/workflows/package-verification.yml
name: Package Verification

on:
  push:
    branches: [ master ]
  pull_request:
    branches: [ master ]

jobs:
  verify-windows:
    runs-on: windows-2022
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Build Release
        run: dotnet build -c Release

      - name: Pack
        run: dotnet pack src/Databento.Client/Databento.Client.csproj -c Release -o ./nupkg

      - name: Test Fresh Install
        run: |
          mkdir test-install
          cd test-install
          dotnet new console
          dotnet add package Databento.Client --version ${{ github.run_number }}.0.0-ci --source ../nupkg
          dotnet build
          ls bin/Debug/net8.0/*.dll
          # Verify VC++ DLLs present
          if not exist "bin\Debug\net8.0\msvcp140.dll" exit 1
          if not exist "bin\Debug\net8.0\vcruntime140.dll" exit 1
          if not exist "bin\Debug\net8.0\vcruntime140_1.dll" exit 1

      - name: Upload Package
        uses: actions/upload-artifact@v3
        with:
          name: nupkg
          path: ./nupkg/*.nupkg
```

---

## Success Criteria

### Immediate (Phase 1)
- ✅ User's specific issue identified and resolved
- ✅ Clear reproduction case documented
- ✅ User confirms application working

### Short Term (Phase 2)
- ✅ README updated with platform requirements
- ✅ UPGRADING.md guide created
- ✅ GitHub issue template updated
- ✅ Zero confusion about version requirements

### Medium Term (Phase 3 + 4)
- ✅ Diagnostic helper available for users
- ✅ Build-time verification warns about missing DLLs
- ✅ Tested on clean Windows VM (no VC++ installed)
- ✅ Package contents verified for every release

### Long Term (Phase 5)
- ✅ Linux support (if demand exists)
- ✅ macOS support (if demand exists)
- ✅ Automated CI testing on fresh VMs
- ✅ Zero reports of DllNotFoundException on Windows v3.0.23+

---

## Timeline

| Phase | Tasks | Effort | Target Date |
|-------|-------|--------|-------------|
| Phase 1 | User support, identify issue | 2 hours | TODAY |
| Phase 2 | Documentation updates | 3 hours | This week |
| Phase 3 | Diagnostic helper, verification | 4 hours | Next week |
| Phase 4 | Testing on clean VM | 2 hours | Next week |
| Phase 5 | Linux/Mac support | 20+ hours | TBD (if needed) |

**Total Estimated Effort**: 11 hours (excluding Linux/Mac support)

---

## Risk Assessment

### Low Risk ✅
- Documentation updates (Phase 2)
- User support (Phase 1)
- No code changes required

### Medium Risk ⚠️
- Adding diagnostic helper (Phase 3)
- MSBuild targets verification (Phase 3)
- Changes to published code, but non-breaking

### High Risk 🔴
- Linux/Mac support (Phase 5)
- New platform builds
- Requires extensive testing

---

## Rollout Plan

### Week 1 (THIS WEEK)
1. Contact user (Phase 1)
2. Update documentation (Phase 2)
3. Start diagnostic helper (Phase 3.1)

### Week 2 (NEXT WEEK)
4. Complete diagnostic helper (Phase 3.1, 3.2)
5. Add MSBuild verification (Phase 3.3)
6. Test on clean Windows VM (Phase 4.1, 4.2)
7. Release v3.0.25-beta with improvements

### Week 3+
8. Monitor for new reports
9. Consider Linux/Mac support based on demand

---

## Appendix: Quick Reference

### User Upgrade Commands
```bash
dotnet add package Databento.Client --version 3.0.24-beta --prerelease
dotnet nuget locals all --clear
dotnet clean && dotnet restore --force && dotnet build
```

### DLL Verification
```bash
ls bin/Debug/net8.0/ | grep -E "(msvcp|vcruntime)"
# Must show: msvcp140.dll, vcruntime140.dll, vcruntime140_1.dll
```

### Cache Location
```bash
# Windows
%USERPROFILE%\.nuget\packages\databento.client\

# Linux/Mac
~/.nuget/packages/databento.client/
```

### Package Inspection
```bash
unzip -l Databento.Client.3.0.24-beta.nupkg | grep -E "runtimes.*\.dll"
```

---

**Plan Created**: November 22, 2025
**Status**: Ready for implementation
**Priority**: HIGH
**Next Step**: Phase 1 - Contact user
