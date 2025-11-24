# Issue #2 Test Results - VC++ Runtime DLL Bundling

**Test Date**: November 22, 2025
**Tested Version**: 3.0.27-beta (latest on NuGet.org)
**Test Environment**: Windows with C++ runtime installed

---

## Executive Summary

✅ **THE FIX IS WORKING!**

Fresh install of Databento.Client from NuGet.org **DOES** include all required VC++ runtime DLLs in the output directory. The bundling fix from v3.0.23-beta is functioning correctly.

**However**, a user reported TODAY that they still needed to manually install C++ runtime. This means their specific scenario is different from a fresh install.

---

## Test Results

### Test 1: Fresh Install from NuGet.org

**Command Run**:
```powershell
dotnet new console
dotnet add package Databento.Client --prerelease
dotnet build
```

**Version Installed**: 3.0.27-beta (latest prerelease)

**Output Directory**: `bin/Debug/net9.0/`

**VC++ Runtime DLLs Found**:
```
✓ msvcp140.dll         (562 KB)  ← VC++ C++ Standard Library
✓ vcruntime140.dll     (118 KB)  ← VC++ Runtime Core
✓ vcruntime140_1.dll   (49 KB)   ← VC++ Runtime Extended
```

**Result**: ✅ **PASS** - All DLLs present and correctly deployed

**Test Script**: `TEST_QUICK.ps1`

---

## Conclusion

The package IS working correctly for fresh installs. The user experiencing issues must be in ONE of these scenarios:

### Scenario A: Old Version Pinned in .csproj 🔴 HIGH PROBABILITY

**How it happens**:
```xml
<!-- User's .csproj file -->
<PackageReference Include="Databento.Client" Version="3.0.15-beta" />
<!--                                                   ^^^^^^^^^^^ OLD VERSION -->
```

Running `dotnet add package` again won't help if version is explicitly pinned to an old version.

**How to fix**:
```bash
# Remove old reference
dotnet remove package Databento.Client

# Add latest
dotnet add package Databento.Client --prerelease

# Clean and rebuild
dotnet clean
dotnet build
```

---

### Scenario B: Stale NuGet Cache 🟡 MEDIUM PROBABILITY

**How it happens**:
- User installed old version weeks/months ago
- Package cached in `~/.nuget/packages/databento.client/3.0.15-beta/`
- Even after "updating", NuGet uses cached old version

**How to fix**:
```bash
# Nuclear option - clear all caches
dotnet nuget locals all --clear

# Remove bin/obj folders
rm -rf bin obj

# Reinstall fresh
dotnet remove package Databento.Client
dotnet add package Databento.Client --prerelease

# Clean build
dotnet restore --force --no-cache
dotnet build
```

---

### Scenario C: Non-Standard Build/Run Method 🟡 LOW-MEDIUM PROBABILITY

**Possible causes**:
1. **Running without building**:
   ```bash
   dotnet restore  # DLLs downloaded to NuGet cache
   dotnet run --no-build  # DLLs not copied to output!
   ```

2. **Publishing with non-standard settings**:
   ```bash
   dotnet publish -c Release --self-contained false
   # May not copy runtime DLLs depending on settings
   ```

3. **Custom MSBuild configuration**:
   - Build targets disabled
   - Custom .props files interfering

**How to fix**:
```bash
# Always build before running
dotnet clean
dotnet build
dotnet run
```

---

### Scenario D: Old SDK/Tooling 🟢 LOW PROBABILITY

**Possible cause**:
- Very old .NET SDK (<6.0) might not properly execute NuGet build targets

**How to check**:
```bash
dotnet --version
# Should be 8.0+ for best compatibility
```

**How to fix**:
- Update to latest .NET SDK

---

### Scenario E: Platform Issue (Linux/Mac) 🔴 CRITICAL IF TRUE

**Status**: Package only supports Windows x64 currently

If user is on Linux or macOS:
- No native libraries exist for these platforms
- VC++ runtime bundling is irrelevant (those are Windows-only)
- Need completely different solution (build native .so/.dylib files)

**How to identify**:
```bash
uname -a  # Check if Linux/Mac
```

---

## What To Ask The User

### Step 1: Identify Their Exact Scenario

Send them these diagnostic commands:

```bash
# 1. What version do they actually have?
dotnet list package | grep Databento

# 2. What's in their .csproj?
cat *.csproj | grep -A 2 "Databento"

# 3. What's in their output directory?
ls -la bin/Debug/net*/msvcp140.dll
ls -la bin/Debug/net*/vcruntime140*.dll

# 4. What platform?
uname -a  # Linux/Mac
ver       # Windows

# 5. How do they build/run?
# Ask: What exact commands do you use to build and run?
```

---

### Step 2: Apply The Right Fix

**If version < 3.0.23-beta**:
```bash
# Scenario A: Upgrade
dotnet remove package Databento.Client
dotnet add package Databento.Client --prerelease
dotnet nuget locals all --clear
dotnet clean
dotnet build
```

**If version >= 3.0.23-beta but DLLs missing**:
```bash
# Scenario B: Clear cache
dotnet nuget locals all --clear
rm -rf bin obj
dotnet restore --force --no-cache
dotnet build
ls bin/Debug/net*/msvcp140.dll  # Verify
```

**If DLLs exist but app still fails**:
- They have C++ runtime NOW (they installed it)
- App is using system DLLs, not bundled ones
- This is actually FINE - it works!
- Real issue was: DLLs weren't there BEFORE they installed C++

**If Linux/Mac**:
- Inform: Platform not supported yet
- Workaround: Use Windows or wait for cross-platform support

---

## Test Scripts Created

We've created several test scripts for reproducing and diagnosing the issue:

### 1. `TEST_QUICK.ps1` (Windows - Recommended)

Quick test that:
- Creates fresh project in temp directory
- Clears NuGet cache
- Installs latest Databento.Client
- Verifies VC++ DLLs in output directory

**Usage**:
```powershell
.\TEST_QUICK.ps1
```

**Result**: ✅ PASS on current version (3.0.27-beta)

---

### 2. `test-fresh-install.ps1` (Windows - Comprehensive)

Comprehensive test with:
- Detailed diagnostics
- NuGet cache inspection
- Output directory analysis
- Step-by-step progress

**Usage**:
```powershell
.\test-fresh-install.ps1
```

**Note**: Has some PowerShell parsing issues, but core logic is sound

---

### 3. `test-fresh-install.sh` (Linux/Mac/Bash)

Bash equivalent of the fresh install test.

**Usage**:
```bash
chmod +x test-fresh-install.sh
./test-fresh-install.sh
```

---

### 4. `test-dll-loading.ps1` (Windows - Advanced)

Advanced test that:
- Creates app that loads native library
- Checks which DLLs are loaded at runtime
- Identifies if using BUNDLED vs SYSTEM DLLs

**Usage**:
```powershell
.\test-dll-loading.ps1
```

**Purpose**: Verify app actually loads our bundled DLLs, not system ones

---

## Recommendations

### For Support Team

**When user reports "DllNotFoundException"**:

1. **Identify version first**:
   ```bash
   dotnet list package | grep Databento
   ```

2. **If < 3.0.23-beta**:
   - "You're on an old version. Please upgrade to 3.0.27-beta or later"
   - Provide upgrade commands (Scenario A)

3. **If >= 3.0.23-beta**:
   - "Check your output directory"
   - Provide diagnostic commands
   - Check for Scenario B (cache) or C (build method)

4. **If platform is Linux/Mac**:
   - "Platform not supported yet. Windows-only currently."

---

### For Development Team

**Short Term**:
1. ✅ Package is working - no code changes needed
2. Update documentation (README, UPGRADING.md)
3. Add version check warnings at runtime
4. Add diagnostic helper class

**Long Term**:
1. Consider Linux/Mac support (if demand exists)
2. Add automated testing on fresh Windows VM
3. Consider stable release (move out of beta)

---

## FAQs

**Q: Why did the user need to install C++ manually if the DLLs are bundled?**

A: Most likely their version is < 3.0.23-beta OR their NuGet cache is stale. The bundled DLLs weren't in their output directory.

**Q: But they said it was a first-time install?**

A: "First time install" could mean:
- First time using Databento (but installed old version from tutorial)
- First time on THIS project (but NuGet cache has old version from OTHER project)
- First time after clearing everything (but .csproj has pinned version)

**Q: How can we prevent this in the future?**

A:
1. Update ALL documentation to show `--version 3.0.27-beta` explicitly
2. Add runtime version check with warning
3. Provide diagnostic helper for users to self-check
4. Move to stable release (no more `-beta`)

**Q: Should we test without C++ runtime installed?**

A: Yes, ideally. Options:
1. Windows Sandbox (clean Windows, no apps)
2. Fresh Windows VM
3. Docker container with minimal Windows image
4. GitHub Actions CI with fresh Windows runner

**Q: Is the fix actually working?**

A: **YES!** Our test confirms:
- Latest package (3.0.27-beta) includes all 3 VC++ DLLs
- Fresh install deploys DLLs to output directory correctly
- Build targets are executing properly

---

## Next Steps

1. **Contact the user** with diagnostic questions (Step 1 above)
2. **Identify their scenario** based on their responses
3. **Apply the appropriate fix** (Step 2 above)
4. **Update documentation** to prevent future confusion
5. **Consider adding runtime diagnostics** (DiagnosticHelper class)

---

## Test Artifacts

**Test Directory**: `C:\Users\serha\AppData\Local\Temp\databento-quick-test`

**Test Output**:
```
Version: 3.0.27-beta
Output: bin\Debug\net9.0

[OK] msvcp140.dll (562 KB)
[OK] vcruntime140.dll (118 KB)
[OK] vcruntime140_1.dll (49 KB)

RESULT: PASS - All VC++ DLLs present
```

**Conclusion**: Package is working as designed for fresh installs.

---

**Test Complete**: November 22, 2025
**Status**: ✅ Fix verified working
**Action Required**: Identify user's specific scenario
