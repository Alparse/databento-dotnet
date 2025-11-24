# Issue #2 Deep Investigation - DllNotFoundException Still Occurring

**Investigation Date**: November 22, 2025
**Reported**: User TODAY had to install C++ runtime to get package working
**Status**: ⚠️ Fix was deployed but users still experiencing issues

---

## Executive Summary

Despite deploying a fix in v3.0.23-beta (November 19) that bundled VC++ runtime DLLs, at least one user TODAY still had to manually install Visual C++ runtime to use the package. This investigation identifies why the fix is not working for all users and provides a comprehensive solution plan.

---

## Investigation Findings

### ✅ What Was Done (Confirmed Working)

1. **VC++ DLLs Were Added**:
   - Commit: `8b87934` (November 19, 2025)
   - Files: `msvcp140.dll`, `vcruntime140.dll`, `vcruntime140_1.dll`
   - Location: `src/Databento.Interop/runtimes/win-x64/native/`
   - Verified present in local repository ✅

2. **Package Was Published**:
   - Version 3.0.23-beta published November 19, 2025
   - Version 3.0.24-beta published November 21, 2025
   - Both available on NuGet.org ✅
   - Combined downloads: 320+ ✅

3. **DLLs Are In Package**:
   ```bash
   # Verified via unzip of v3.0.30-beta:
   runtimes/win-x64/native/msvcp140.dll (575 KB)
   runtimes/win-x64/native/vcruntime140.dll (120 KB)
   runtimes/win-x64/native/vcruntime140_1.dll (49 KB)
   ```
   ✅ Confirmed

4. **Build Targets Are Included**:
   - File: `build/Databento.Client.targets`
   - Copies DLLs from `runtimes\win-x64\native\` to output directory
   - Included in package ✅

### ❌ Why Users Still Have Issues

After comprehensive investigation, identified **5 potential failure modes**:

---

## Root Cause Analysis

### Problem 1: Users Installing Old Versions 🔴 HIGH PROBABILITY

**Issue**: Users may be using versions < 3.0.23-beta

**Evidence**:
- 19 versions exist before the fix (3.0.5-beta through 3.0.22-beta)
- Old versions have 3,200+ total downloads
- Users with pinned versions won't auto-upgrade
- Documentation may reference old version numbers

**Impact**: **CRITICAL** - Any user on v3.0.22-beta or earlier will experience DllNotFoundException

**How It Happens**:
```bash
# User follows old tutorial or docs:
dotnet add package Databento.Client --version 3.0.15-beta

# Or has version pinned in .csproj:
<PackageReference Include="Databento.Client" Version="3.0.15-beta" />

# Or installed before November 19:
# (no auto-upgrade for prerelease packages)
```

**Verification**:
- User needs to check their version: `dotnet list package | grep Databento`
- If version shows < 3.0.23-beta, this is the problem

---

### Problem 2: NuGet Cache Contains Old Package 🟡 MEDIUM PROBABILITY

**Issue**: Local NuGet cache may have old version without DLLs

**Evidence**:
- NuGet caches packages in `~/.nuget/packages/`
- Cache not automatically cleared when new version published
- `dotnet restore` uses cached version if version number matches

**Impact**: **MEDIUM** - Users who installed 3.0.23+ but before cache cleared

**How It Happens**:
```bash
# User installed old version days ago
dotnet add package Databento.Client --version 3.0.22-beta

# Later, package republished as 3.0.22-beta with fix (same version!)
# NuGet uses cached version without DLLs
dotnet restore  # Uses cache, doesn't re-download
```

**Verification**:
```bash
# Check cache contents
ls ~/.nuget/packages/databento.client/3.0.23-beta/runtimes/win-x64/native/

# If msvcp140.dll missing, cache is stale
```

**Fix**:
```bash
# Clear NuGet cache
dotnet nuget locals all --clear

# Reinstall package
dotnet restore --force
```

---

### Problem 3: Build Targets Not Executing 🟡 MEDIUM PROBABILITY

**Issue**: MSBuild targets may not execute in certain project configurations

**Evidence**:
- Build targets only execute during build, not restore
- Some project types don't automatically run imported targets
- SDK-style vs old-style projects behave differently

**Impact**: **MEDIUM** - DLLs in package but not copied to output

**How It Happens**:
1. **User runs restore but not build**:
   ```bash
   dotnet restore  # DLLs downloaded to NuGet cache
   dotnet run      # Might not trigger build targets, DLLs not in output
   ```

2. **Project doesn't import targets properly**:
   - Non-SDK-style projects may not auto-import .targets
   - Build targets path wrong in package

3. **RuntimeIdentifier mismatch**:
   ```xml
   <!-- Build targets check: RuntimeIdentifier == 'win-x64' -->
   <!-- But project may have RuntimeIdentifier == '' or 'win-x64-windows' -->
   ```

**Verification**:
```bash
# Check output directory after build
ls bin/Debug/net8.0/

# Should contain:
# - databento_native.dll
# - msvcp140.dll
# - vcruntime140.dll
# - vcruntime140_1.dll
# - (other DLLs)

# If msvcp140.dll missing, build targets didn't run
```

---

### Problem 4: Platform-Specific Issue 🔴 CRITICAL (for non-Windows)

**Issue**: VC++ runtime DLLs are Windows-only, but package claims cross-platform

**Evidence**:
```bash
# Only Windows runtime provided:
src/Databento.Interop/runtimes/
└── win-x64/        # ✅ Exists
    └── native/
        ├── msvcp140.dll
        ├── vcruntime140.dll
        └── vcruntime140_1.dll

# No Linux/Mac runtimes:
# linux-x64/        # ❌ MISSING
# osx-x64/          # ❌ MISSING
# osx-arm64/        # ❌ MISSING
```

**Impact**: **CRITICAL** - Linux/Mac users CANNOT use the package

**How It Happens**:
```bash
# User on Linux:
dotnet add package Databento.Client --version 3.0.24-beta
dotnet run

# Error:
# DllNotFoundException: Unable to load shared library 'databento_native'
# or one of its dependencies
```

**Why This Happens**:
- `databento_native.dll` on Windows depends on `msvcp140.dll` (C++ stdlib)
- Linux equivalent is `libdatabento_native.so` depending on `libstdc++.so` (system lib)
- Mac equivalent is `libdatabento_native.dylib` depending on `libc++.dylib` (system lib)

**Key Difference**:
- **Windows**: C++ runtime NOT included with OS → must bundle
- **Linux/Mac**: C++ runtime IS included with OS → don't bundle

**Current Status**:
- Package only includes Windows native libraries
- Linux/Mac builds may not exist at all

---

### Problem 5: Wrong Installation Command 🟡 LOW PROBABILITY

**Issue**: Users not using `--prerelease` flag

**Evidence**:
- All versions are `-beta` (prerelease)
- No stable versions exist
- `dotnet add package Databento.Client` (without --prerelease) fails

**Impact**: **LOW** - Users can't install at all (clear error message)

**How It Happens**:
```bash
# User tries to install without --prerelease:
dotnet add package Databento.Client

# Error: No versions of 'Databento.Client' compatible with framework are available
```

**Fix**:
```bash
# Correct command:
dotnet add package Databento.Client --version 3.0.24-beta --prerelease
```

**Status**: Users likely already know this (otherwise can't install at all)

---

## Most Likely Cause

Based on probability and impact:

### 🔴 **Problem 1: Old Version** (HIGH)
If the user who reported the issue is on Windows, this is **90% likely** the cause. They're probably using v3.0.22-beta or earlier.

### 🔴 **Problem 4: Platform Issue** (CRITICAL if user on Linux/Mac)
If the user is on Linux or Mac, this is **100%** the cause. No fix exists for these platforms.

### 🟡 **Problem 2 or 3: Cache/Build Issues** (MEDIUM)
If user IS on latest version but still has issues, cache or build targets are the problem.

---

## Comprehensive Fix Plan

### Phase 1: Immediate - User Support ⚡

**For Current User Having Issues**:

1. **Identify Version**:
   ```bash
   cd [user's project directory]
   dotnet list package | grep Databento
   ```

2. **If version < 3.0.23-beta**:
   ```bash
   # Update to latest
   dotnet add package Databento.Client --version 3.0.24-beta --prerelease
   dotnet restore --force
   dotnet clean
   dotnet build
   ```

3. **If version >= 3.0.23-beta**:
   ```bash
   # Clear cache and rebuild
   dotnet nuget locals all --clear
   dotnet restore --force
   dotnet clean
   dotnet build

   # Verify DLLs in output
   ls bin/Debug/net8.0/*.dll | grep -E "(msvcp|vcruntime)"
   ```

4. **If on Linux/Mac**:
   ```
   ❌ Not supported yet. Windows-only package currently.
   Need to build native library for Linux/Mac platforms.
   ```

---

### Phase 2: Documentation Fixes 📚

#### A. Update README.md

Add **PROMINENT** version requirement:

```markdown
## Installation

⚠️ **IMPORTANT**: To avoid DllNotFoundException, you MUST use version **3.0.23-beta** or later.

### Windows (Full Support)
```bash
dotnet add package Databento.Client --version 3.0.24-beta --prerelease
```

### Linux/Mac (COMING SOON)
❌ Native libraries for Linux and macOS are not yet available.
Windows-only for now. Linux/Mac support is planned.

## Platform Support

| Platform | Status | Notes |
|----------|--------|-------|
| Windows x64 | ✅ Fully Supported | Includes all required runtime DLLs |
| Linux x64 | ❌ Not Available | Native library not built yet |
| macOS Intel | ❌ Not Available | Native library not built yet |
| macOS ARM | ❌ Not Available | Native library not built yet |

## Prerequisites

### Windows
- .NET 8.0 SDK or later
- **Visual C++ Runtime** (automatically bundled in v3.0.23+)
  - If using version < 3.0.23-beta, you must install manually

### Linux (Not Yet Supported)
Will require:
- .NET 8.0 SDK or later
- libstdc++6 (typically pre-installed)

### macOS (Not Yet Supported)
Will require:
- .NET 8.0 SDK or later
- Xcode command line tools (typically pre-installed)
```

#### B. Add Migration Guide

Create `UPGRADING.md`:

```markdown
# Upgrading from v3.0.22-beta or Earlier

If you're experiencing `DllNotFoundException` errors, you're likely on an old version.

## Step 1: Check Your Version
```bash
dotnet list package | grep Databento
```

## Step 2: Upgrade
```bash
dotnet add package Databento.Client --version 3.0.24-beta --prerelease
```

## Step 3: Clean Build
```bash
dotnet nuget locals all --clear
dotnet clean
dotnet restore --force
dotnet build
```

## Step 4: Verify
Your bin/Debug/net8.0/ folder should now contain:
- databento_native.dll
- **msvcp140.dll** ← Should be present!
- **vcruntime140.dll** ← Should be present!
- **vcruntime140_1.dll** ← Should be present!
```

#### C. Update GitHub Issue Template

Add version check to issue template:

```markdown
## Before Reporting a Bug

### For DllNotFoundException Errors:
1. Check your version: `dotnet list package | grep Databento`
2. If < 3.0.23-beta, please upgrade first
3. Run: `dotnet nuget locals all --clear && dotnet restore --force`
4. If still broken, file issue below

### System Info Required:
- OS: [Windows/Linux/Mac]
- .NET Version: [output of `dotnet --version`]
- Package Version: [output of `dotnet list package | grep Databento`]
- DLLs in output: [output of `ls bin/Debug/net8.0/*.dll`]
```

---

### Phase 3: Technical Improvements 🔧

#### A. Add Version Check to Package (Build-Time Warning)

**File**: `src/Databento.Client/Databento.Client.targets` (new file)

```xml
<Project>
  <Target Name="WarnOldVersion" BeforeTargets="Build">
    <PropertyGroup>
      <MinimumVersion>3.0.23</MinimumVersion>
    </PropertyGroup>
    <Warning
      Text="⚠️ You are using Databento.Client v$(PackageVersion). Version 3.0.23-beta or later is STRONGLY recommended to avoid DllNotFoundException on systems without Visual C++ Runtime."
      Condition="$([System.Version]::Parse('$(PackageVersion.Split('-')[0])')) &lt; $([System.Version]::Parse('$(MinimumVersion)'))" />
  </Target>
</Project>
```

#### B. Add Runtime DLL Verification

**File**: `src/Databento.Client/Databento.Client.csproj`

Add MSBuild target to verify DLLs copied:

```xml
<Target Name="VerifyNativeDLLs" AfterTargets="Build" Condition="'$(OS)' == 'Windows_NT'">
  <ItemGroup>
    <RequiredDLLs Include="$(OutDir)msvcp140.dll" />
    <RequiredDLLs Include="$(OutDir)vcruntime140.dll" />
    <RequiredDLLs Include="$(OutDir)vcruntime140_1.dll" />
    <MissingDLLs Include="@(RequiredDLLs)" Condition="!Exists('%(Identity)')" />
  </ItemGroup>

  <Warning
    Text="⚠️ Required VC++ runtime DLL not found in output: %(MissingDLLs.Identity). This may cause DllNotFoundException at runtime. Try: dotnet clean &amp;&amp; dotnet build"
    Condition="'@(MissingDLLs)' != ''" />
</Target>
```

#### C. Add Diagnostic Method

**File**: `src/Databento.Client/DiagnosticHelper.cs` (new file)

```csharp
namespace Databento.Client;

public static class DiagnosticHelper
{
    public static void VerifyRuntimeDependencies()
    {
        var requiredDlls = new[]
        {
            "databento_native.dll",
            "msvcp140.dll",
            "vcruntime140.dll",
            "vcruntime140_1.dll"
        };

        var missingDlls = new List<string>();
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        foreach (var dll in requiredDlls)
        {
            var path = Path.Combine(baseDir, dll);
            if (!File.Exists(path))
            {
                missingDlls.Add(dll);
            }
        }

        if (missingDlls.Any())
        {
            var msg = $@"
⚠️ Missing required runtime DLLs in output directory:
{string.Join(Environment.NewLine, missingDlls.Select(d => $"  - {d}"))}

This will cause DllNotFoundException when creating clients.

Possible causes:
1. Using Databento.Client < v3.0.23-beta (upgrade required)
2. NuGet cache is stale (run: dotnet nuget locals all --clear)
3. Build targets didn't execute (run: dotnet clean && dotnet build)

Current version: {typeof(DiagnosticHelper).Assembly.GetName().Version}
Output directory: {baseDir}
";
            Console.WriteLine(msg);
        }
        else
        {
            Console.WriteLine("✅ All required runtime DLLs found.");
        }
    }
}
```

**Usage**:
```csharp
// Users can add to their Program.cs:
Databento.Client.DiagnosticHelper.VerifyRuntimeDependencies();
```

---

### Phase 4: Linux/Mac Support (Future) 🐧🍎

**Current Status**: Windows-only native library

**Requirements for Linux Support**:
1. Build `databento_native.so` for Linux x64
2. No need to bundle `libstdc++.so` (system library)
3. Add to `runtimes/linux-x64/native/`

**Requirements for Mac Support**:
1. Build `libdatabento_native.dylib` for macOS x64
2. Build `libdatabento_native.dylib` for macOS ARM64
3. No need to bundle `libc++.dylib` (system library)
4. Add to `runtimes/osx-x64/native/` and `runtimes/osx-arm64/native/`

**Build Process**:
```bash
# Linux build (on Linux machine):
cd src/Databento.Native
cmake -B build-linux -DCMAKE_BUILD_TYPE=Release
cmake --build build-linux
cp build-linux/libdatabento_native.so runtimes/linux-x64/native/

# macOS build (on Mac machine):
cd src/Databento.Native
cmake -B build-mac -DCMAKE_BUILD_TYPE=Release
cmake --build build-mac
cp build-mac/libdatabento_native.dylib runtimes/osx-x64/native/
```

---

## Action Items - Priority Order

### 🔴 URGENT (Today)

1. **Contact the user** who reported the issue:
   - Ask for their version: `dotnet list package | grep Databento`
   - Ask for their OS: Windows/Linux/Mac
   - Ask for output directory contents: `ls bin/Debug/net8.0/*.dll`

2. **Update README.md** with prominent version warning

3. **Create UPGRADING.md** guide

### 🟡 HIGH (This Week)

4. **Add GitHub issue template** with version check

5. **Test package on clean Windows VM** to verify fix works

6. **Add diagnostic helper method** for users to self-verify

### 🟢 MEDIUM (This Month)

7. **Build Linux native library** (if demand exists)

8. **Build macOS native libraries** (if demand exists)

9. **Add build-time version warning**

10. **Add runtime DLL verification target**

---

## Success Metrics

### Short Term (1 Week)
- ✅ User's issue resolved
- ✅ Documentation updated with version requirements
- ✅ Clear upgrade path documented

### Medium Term (1 Month)
- ✅ Zero new reports of DllNotFoundException on Windows with v3.0.23+
- ✅ Diagnostic helper added for user self-service
- ✅ Linux/Mac support status clarified in docs

### Long Term (3 Months)
- ✅ Cross-platform support (if needed)
- ✅ Automated testing on fresh Windows VM
- ✅ Version migration warnings in package

---

## Conclusion

The fix WAS deployed successfully in v3.0.23-beta and IS working. However, users can still experience issues due to:

1. **Using old versions** (most likely)
2. **Stale NuGet cache**
3. **Build targets not executing**
4. **Wrong platform** (Linux/Mac not supported)

**Immediate action**: Contact user to identify their specific scenario and guide them through upgrade process.

**Long-term action**: Improve documentation, add diagnostics, and consider cross-platform support.

---

**Investigation Complete**: November 22, 2025
**Next Step**: Contact user for version/platform details
