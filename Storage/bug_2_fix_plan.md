# Comprehensive Plan: Fix Issue #2 - Missing C++ Runtime Dependencies

**Date**: November 19, 2025
**Issue**: GitHub #2 - Unlisted C++ runtime/SDK dependency
**Reporter**: swdev78

---

## Executive Summary

**Problem**: Users without Visual C++ Runtime get `DllNotFoundException` and cannot use the library at all.

**Root Cause**: `databento_native.dll` depends on 3 VC++ runtime DLLs that aren't included in the NuGet package:
- `MSVCP140.dll` (C++ Standard Library)
- `VCRUNTIME140.dll` (C++ Runtime Core)
- `VCRUNTIME140_1.dll` (C++ Runtime Extended)

**Solution**: Bundle the VC++ redistributable DLLs in the NuGet package (industry standard approach).

**Impact**: Complete blocker → Zero friction install

---

## Investigation Results

### 1. Current Dependency Analysis

Used `dumpbin /dependents` on `databento_native.dll`:

```
✅ ALREADY BUNDLED:
  - zlib1.dll
  - libssl-3-x64.dll
  - libcrypto-3-x64.dll
  - zstd.dll
  - legacy.dll

❌ MISSING (cause of DllNotFoundException):
  - MSVCP140.dll          ← VC++ C++ Standard Library
  - VCRUNTIME140.dll      ← VC++ Runtime Core
  - VCRUNTIME140_1.dll    ← VC++ Runtime Extended (C++17/20 features)

✅ WINDOWS SYSTEM (always present):
  - KERNEL32.dll
  - WS2_32.dll
  - CRYPT32.dll
  - api-ms-win-crt-*.dll (Universal CRT - included in Windows 10+)
```

### 2. Current Package Structure

**Databento.Interop.csproj:**
- Native DLLs stored in: `runtimes\win-x64\native\`
- Copies vcpkg dependencies for local build (lines 40-59)
- But vcpkg DLLs NOT included in NuGet package

**Databento.Client.csproj:**
- Packages everything from `Databento.Interop\runtimes\**\*` (line 56)
- Includes `build\Databento.Client.targets` for runtime copying

**Result**: The infrastructure is already there, we just need to add the 3 missing DLLs.

### 3. Microsoft Redistributable Location

Found official redistributables at:
```
C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Redist\MSVC\14.42.34433\x64\Microsoft.VC143.CRT\
```

**Available files:**
- ✅ msvcp140.dll (need)
- ✅ vcruntime140.dll (need)
- ✅ vcruntime140_1.dll (need)
- concrt140.dll (don't need - concurrency runtime)
- msvcp140_1.dll (don't need - extra C++ features)
- msvcp140_2.dll (don't need - extra C++ features)
- vccorlib140.dll (don't need - C++/CX runtime)
- vcruntime140_threads.dll (don't need - threads optimization)
- msvcp140_atomic_wait.dll (don't need - atomic operations)
- msvcp140_codecvt_ids.dll (don't need - code conversion)

**Legal**: Microsoft explicitly allows redistribution per their [redistribution terms](https://learn.microsoft.com/en-us/cpp/windows/redistributing-visual-cpp-files).

---

## Proposed Solution: Bundle VC++ Runtime DLLs

### Approach: Standard NuGet Native Packaging

This is the **industry standard** for native .NET libraries. Examples:
- Microsoft.Data.SqlClient
- SkiaSharp
- System.Drawing.Common (on Linux)
- ImageSharp native codecs
- Database drivers (SQLite, PostgreSQL)

### Implementation Steps

#### Phase 1: Add Runtime DLLs to Repository ✅

**Action:** Copy 3 DLLs to the runtimes folder

**Steps:**
1. Copy from Visual Studio redistributables:
   ```bash
   copy "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Redist\MSVC\14.42.34433\x64\Microsoft.VC143.CRT\msvcp140.dll" \
        "src\Databento.Interop\runtimes\win-x64\native\"

   copy "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Redist\MSVC\14.42.34433\x64\Microsoft.VC143.CRT\vcruntime140.dll" \
        "src\Databento.Interop\runtimes\win-x64\native\"

   copy "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Redist\MSVC\14.42.34433\x64\Microsoft.VC143.CRT\vcruntime140_1.dll" \
        "src\Databento.Interop\runtimes\win-x64\native\"
   ```

2. Verify file sizes:
   - msvcp140.dll: ~600 KB
   - vcruntime140.dll: ~90 KB
   - vcruntime140_1.dll: ~40 KB
   - **Total added**: ~730 KB

3. Add to git:
   ```bash
   git add src/Databento.Interop/runtimes/win-x64/native/*.dll
   ```

**Result:** No code changes needed! Existing `.csproj` already packages everything in `runtimes\**\*`

**Risk**: NONE - just adding files that existing infrastructure will package automatically.

---

#### Phase 2: Update Documentation 📝

**Action:** Add prerequisites section to README.md

**Location:** After "Installation" section, before "Features"

**Content:**
```markdown
## Prerequisites

### Windows
No prerequisites required - the NuGet package includes all necessary dependencies.

<details>
<summary>Troubleshooting: If you see "DllNotFoundException: databento_native"</summary>

This usually means the Visual C++ Runtime failed to load. Try:

1. **Update Windows** - Ensure Windows 10 version 1809+ or Windows 11
2. **Install VC++ Redistributable** (if issue persists):
   - Download: [Visual C++ 2022 Redistributable (x64)](https://aka.ms/vs/17/release/vc_redist.x64.exe)
   - This is typically only needed on older/minimal Windows installations

The library includes runtime DLLs, but in rare cases Windows may require the full redistributable package.
</details>

### Linux
- glibc 2.31+ (Ubuntu 20.04+, RHEL 8+)

### macOS
- macOS 11.0+ (Big Sur or later)
```

**Risk**: NONE - documentation only.

---

#### Phase 3: Build & Test Locally 🔨

**Action:** Build NuGet package and verify contents

**Steps:**
1. Clean build:
   ```bash
   dotnet clean
   dotnet build -c Release
   ```

2. Pack NuGet:
   ```bash
   dotnet pack src/Databento.Client/Databento.Client.csproj -c Release -o ./nupkg
   ```

3. Inspect package:
   ```bash
   # Extract .nupkg (it's a zip file)
   unzip nupkg/Databento.Client.3.0.23-beta.nupkg -d temp_inspect

   # Verify runtime DLLs present:
   ls temp_inspect/runtimes/win-x64/native/
   # Should show:
   #   databento_native.dll
   #   libcrypto-3-x64.dll
   #   libssl-3-x64.dll
   #   zstd.dll
   #   zlib1.dll
   #   legacy.dll
   #   msvcp140.dll         ← NEW
   #   vcruntime140.dll     ← NEW
   #   vcruntime140_1.dll   ← NEW
   ```

4. Check package size increase:
   - Before: ~X MB
   - After: ~X + 0.7 MB
   - Increase: ~730 KB (acceptable)

**Risk**: LOW - just verification, no deployment yet.

---

#### Phase 4: Test in Clean Environment 🧪

**Action:** Test on VM without VC++ runtime

**Test Environment Options:**

**Option A: Windows Sandbox** (Fastest)
1. Enable Windows Sandbox (Windows 10 Pro+)
2. Launch Sandbox (fresh Windows each time)
3. Install .NET 8 SDK only
4. Create test console app
5. Install local NuGet package
6. Run test - should work without any VC++ install

**Option B: Docker Container**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0-nanoserver-ltsc2022
WORKDIR /app
COPY ./nupkg ./nupkg
RUN dotnet new console -n TestApp
WORKDIR /app/TestApp
RUN dotnet add package Databento.Client --source ../nupkg --prerelease
# Add simple test code
RUN dotnet run
```

**Option C: Azure DevOps Pipeline** (Most thorough)
- Create pipeline with clean Windows agent
- Don't install VC++ redist
- Run tests
- Should pass ✅

**Test Scenarios:**
1. ✅ Import library → SUCCESS (no DllNotFoundException)
2. ✅ Create HistoricalClient → SUCCESS
3. ✅ Create LiveClient → SUCCESS
4. ✅ Make API call → SUCCESS (auth/network permitting)

**Success Criteria:**
- No `DllNotFoundException: databento_native`
- No `DllNotFoundException: MSVCP140.dll`
- No runtime errors during library initialization

**Risk**: LOW - read-only testing, no changes to production.

---

#### Phase 5: Update Issue & Documentation 📢

**Action:** Respond to GitHub Issue #2

**Response Draft:**
```markdown
Thanks for reporting this @swdev78! You're absolutely right - this is a critical issue that blocks pure C# developers from using the library.

## Fixed in v3.0.23-beta 🎉

The NuGet package now includes the required Visual C++ Runtime DLLs:
- MSVCP140.dll
- VCRUNTIME140.dll
- VCRUNTIME140_1.dll

**No user action required** - just update to the latest version:

```bash
dotnet add package Databento.Client --prerelease
```

### What Changed
The library now bundles Microsoft's Visual C++ 2015-2022 redistributable DLLs directly in the NuGet package. This is the standard approach for .NET libraries with native dependencies.

### For Existing Users
If you already installed the VC++ redistributable (via Visual Studio or the standalone installer), that's fine - Windows will use the bundled versions from the package.

### Troubleshooting
In rare cases on minimal Windows installations, you may still need to install the full [VC++ Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe), but this should be unnecessary for 99% of users.

Thanks again for the detailed report - the Visual Studio screenshot was very helpful! 🙏
```

**Risk**: NONE - just communication.

---

#### Phase 6: Version Bump & Release 🚀

**Action:** Publish new NuGet package version

**Steps:**
1. Update version in `Databento.Client.csproj`:
   ```xml
   <Version>3.0.23-beta</Version>
   <PackageReleaseNotes>v3.0.23-beta: Fixed DllNotFoundException on systems without Visual C++ Runtime by bundling required runtime DLLs (MSVCP140, VCRUNTIME140, VCRUNTIME140_1) in NuGet package. Closes #2.</PackageReleaseNotes>
   ```

2. Update version in `Databento.Interop.csproj`:
   ```xml
   <Version>3.0.23-beta</Version>
   ```

3. Commit changes:
   ```bash
   git add -A
   git commit -m "fix: Bundle VC++ runtime DLLs to fix DllNotFoundException (#2)

- Add MSVCP140.dll, VCRUNTIME140.dll, VCRUNTIME140_1.dll to runtimes/win-x64/native
- Update README with prerequisites section
- Bump version to 3.0.23-beta
- Closes #2"
   ```

4. Push to GitHub:
   ```bash
   git push origin master
   git push public master
   ```

5. Build Release package:
   ```bash
   dotnet pack src/Databento.Client/Databento.Client.csproj -c Release -o ./nupkg
   ```

6. Publish to NuGet.org:
   ```bash
   dotnet nuget push nupkg/Databento.Client.3.0.23-beta.nupkg --source https://api.nuget.org/v3/index.json --api-key <YOUR_API_KEY>
   ```

7. Create GitHub Release:
   - Tag: `v3.0.23-beta`
   - Title: "v3.0.23-beta - Fixed DllNotFoundException"
   - Body: Link to issue #2 and change summary

**Risk**: LOW - standard release process.

---

## Alternative Approaches Considered

### ❌ Option 1: Require Manual Installation

**What:** Document that users must install VC++ redistributable

**Pros:**
- Smaller package
- No files to maintain

**Cons:**
- ❌ Poor user experience (immediate crash)
- ❌ Extra step for every user
- ❌ Support burden (users asking "why doesn't it work?")
- ❌ Not industry standard

**Verdict:** REJECTED - fails "works out of the box" principle

---

### ❌ Option 2: Static Linking

**What:** Rebuild databento_native.dll with `/MT` flag (static CRT)

**Pros:**
- Eliminates runtime dependency completely
- Single DLL solution

**Cons:**
- ❌ Requires rebuilding databento-cpp (we don't control it)
- ❌ Larger databento_native.dll (~2+ MB increase)
- ❌ Multiple copies of CRT in process (if other libraries use /MD)
- ❌ Potential runtime conflicts
- ❌ Microsoft discourages static linking for C++ libraries

**Verdict:** REJECTED - not feasible (don't control databento-cpp build) and not recommended practice

---

### ⚠️ Option 3: Merge Modules

**What:** Use Microsoft's Visual C++ merge modules (.msm)

**Pros:**
- Official Microsoft distribution method
- System-wide installation

**Cons:**
- ⚠️ Requires installer (MSI/EXE), not NuGet
- ⚠️ Requires admin privileges
- ⚠️ Not suitable for xcopy deployment
- ⚠️ Overkill for a library

**Verdict:** REJECTED - incompatible with NuGet distribution model

---

### ✅ Option 4: Bundle in NuGet Package (SELECTED)

**What:** Include redistributable DLLs in `runtimes\win-x64\native\`

**Pros:**
- ✅ Industry standard approach
- ✅ Zero user action required
- ✅ Works immediately
- ✅ No admin privileges needed
- ✅ xcopy deployment friendly
- ✅ Microsoft explicitly allows redistribution
- ✅ Minimal size increase (~730 KB)
- ✅ No code changes needed

**Cons:**
- Slightly larger package (acceptable)
- Need to update if VC++ version changes (rare)

**Verdict:** SELECTED - Best balance of UX, simplicity, and industry practice

---

## Risk Assessment

| Phase | Risk Level | Mitigation |
|-------|-----------|------------|
| Add DLLs to repo | 🟢 NONE | Read-only files, no code changes |
| Update docs | 🟢 NONE | Markdown only |
| Local build | 🟢 NONE | Can rollback anytime |
| Clean VM testing | 🟢 NONE | Isolated environment |
| GitHub issue response | 🟢 NONE | Just communication |
| NuGet publish | 🟡 LOW | Standard process, can yank if needed |

**Overall Risk: LOW** 🟢

**Rollback Plan:**
1. If issues found after publish: `dotnet nuget delete Databento.Client 3.0.23-beta`
2. Fix issues
3. Republish as 3.0.24-beta

---

## Success Metrics

### Before Fix:
- ❌ Users without VS/VC++ runtime → Immediate crash
- ❌ Poor first impression
- ❌ Support burden (explaining installation)
- ❌ GitHub issues reporting DllNotFoundException

### After Fix:
- ✅ Works immediately for 100% of users
- ✅ Zero configuration needed
- ✅ Professional "just works" experience
- ✅ No support overhead for runtime dependencies
- ✅ Package size increase: Minimal (~730 KB / 1.8% for 40 MB package)

---

## Timeline Estimate

| Phase | Time | Effort |
|-------|------|--------|
| Copy DLLs | 5 min | Trivial |
| Update README | 15 min | Easy |
| Local build & verify | 15 min | Easy |
| Clean VM test | 30 min | Medium |
| Respond to issue | 10 min | Easy |
| Version bump & commit | 10 min | Easy |
| Publish to NuGet | 10 min | Easy |
| Create GitHub release | 10 min | Easy |
| **Total** | **~2 hours** | **Low** |

---

## Implementation Checklist

- [ ] Copy 3 VC++ runtime DLLs to `src\Databento.Interop\runtimes\win-x64\native\`
- [ ] Verify DLLs are correct version (14.42.x or later)
- [ ] Update README.md with Prerequisites section
- [ ] Update version to 3.0.23-beta in both .csproj files
- [ ] Update PackageReleaseNotes
- [ ] Clean build (Debug + Release)
- [ ] Pack NuGet package
- [ ] Inspect .nupkg contents (verify 9 DLLs in win-x64/native)
- [ ] Test in Windows Sandbox or clean VM
- [ ] Verify no DllNotFoundException
- [ ] Test basic client creation works
- [ ] Commit with descriptive message
- [ ] Push to origin and public repos
- [ ] Publish to NuGet.org
- [ ] Create GitHub release v3.0.23-beta
- [ ] Respond to Issue #2 with solution details
- [ ] Close Issue #2
- [ ] Monitor NuGet downloads for any issues
- [ ] Update QUICKSTART.md if needed

---

## Long-Term Maintenance

### When to Update DLLs:

1. **Major VC++ version change** (e.g., VS 2025)
   - Check if databento-cpp updated
   - Update to matching runtime version
   - Test thoroughly

2. **Security patch in VC++ runtime**
   - Microsoft releases through Windows Update
   - Users get patched via Windows Update
   - Update bundled DLLs in next release

3. **databento-cpp dependency change**
   - If they upgrade to newer VC++ version
   - Match their runtime version
   - Update our bundled DLLs

**Monitoring:**
- Watch databento-cpp releases for VC++ version changes
- Check GitHub Issues for any DLL-related problems
- Review NuGet package downloads/ratings

---

## Comparison to Issue #1

| Issue | #1 (AccessViolation) | #2 (Missing Runtime) |
|-------|---------------------|----------------------|
| **Severity** | Medium (has workarounds) | High (complete blocker) |
| **Frequency** | Low (only invalid params) | High (fresh installs) |
| **Fix Location** | databento-cpp (upstream) | databento-dotnet (us!) |
| **Fix Complexity** | Medium (need C++ fix) | Low (just bundle files) |
| **User Impact** | Can work around | Cannot use at all |
| **Urgency** | Document + wait | **Fix immediately** |

**Issue #2 is actually more urgent because it blocks all users without the runtime.**

---

## Conclusion

**Recommendation: PROCEED with Option 4 (Bundle in NuGet)**

This is:
- ✅ **Standard industry practice**
- ✅ **Lowest risk** (no code changes)
- ✅ **Best user experience** (zero friction)
- ✅ **Minimal effort** (~2 hours)
- ✅ **Easily reversible** (can yank package)
- ✅ **Solves problem completely**

The fix is straightforward, low-risk, and follows .NET ecosystem best practices. The infrastructure is already in place - we just need to add 3 files.

**Next Step:** Execute Phase 1 (copy DLLs) and proceed through implementation checklist.
