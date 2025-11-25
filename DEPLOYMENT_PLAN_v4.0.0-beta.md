# Deployment Plan: v4.0.0-beta Release

**Release Date**: TBD
**Previous Version**: v3.0.29-beta
**New Version**: v4.0.0-beta
**Type**: Major version (breaking changes)

---

## 🚨 Breaking Change Summary

### What's Breaking

**`InstrumentDefMessage.RawInstrumentId` changed from `uint` (32-bit) to `ulong` (64-bit)**

**Why?** Some venues like Eurex (XEUR.EOBI) use 64-bit instrument IDs that exceed `uint.MaxValue` (4,294,967,295).

**Example**: Eurex spread ID `0x010002B100000060` = 72,060,553,270,394,976

---

## ✅ Quick Migration Guide (For Users)

### Who Is Affected?

You're affected if you:
- ✅ Explicitly declare `uint` for RawInstrumentId
- ✅ Store RawInstrumentId in a database (INT column)
- ✅ Serialize/deserialize InstrumentDefMessage
- ✅ Pass RawInstrumentId to APIs expecting 32-bit integers

### Who Is NOT Affected?

You're fine if you:
- ✅ Use `var` (type inference)
- ✅ Don't use RawInstrumentId at all
- ✅ Only query US equities (values fit in uint)

### How to Fix (30 seconds)

```csharp
// ❌ OLD (v3.0.29-beta and earlier)
uint rawId = instrumentDef.RawInstrumentId;

// ✅ NEW (v4.0.0-beta)
ulong rawId = instrumentDef.RawInstrumentId;
```

**Database migration:**
```sql
-- If you store RawInstrumentId in SQL Server:
ALTER TABLE Instruments
  ALTER COLUMN RawInstrumentId BIGINT;
```

**That's it!** Most code will work unchanged due to implicit conversions.

---

## 📋 Release Checklist

### Phase 1: Pre-Release Verification ✅

- [ ] **Verify all changes committed**
  - RawInstrumentId uint → ulong in `InstrumentDefMessage.cs`
  - ReadRawInstrumentId() method updated in `Record.cs`
  - All new examples created and working

- [ ] **Build verification**
  ```bash
  dotnet build databento-dotnet.sln -c Release
  ```
  - [ ] Zero errors
  - [ ] Document any warnings

- [ ] **Test suite**
  - [ ] Run all existing tests (if any)
  - [ ] Verify XEUR.EOBI data loads (TestsScratchpad.Internal)
  - [ ] Verify InstrumentDefTest.v4 passes
  - [ ] Run new examples:
    - [ ] IntradayReplay2.Example
    - [ ] Get_Most_Recent_Market_Open.Example
    - [ ] List_Available_Schemas.Example

- [ ] **Example verification**
  - [ ] All examples build without errors
  - [ ] Run at least 3-5 examples to verify runtime behavior
  - [ ] Historical.Readme.Example works
  - [ ] LiveStreaming examples work

### Phase 2: Version Number Updates 📝

Update version numbers in these files:

- [ ] **README.md** (line 3, line 34)
  ```markdown
  [![NuGet](https://img.shields.io/badge/NuGet-v4.0.0--beta-blue)]
  <PackageReference Include="Databento.Client" Version="4.0.0-beta" />
  ```

- [ ] **src/Databento.Client/Databento.Client.csproj**
  ```xml
  <Version>4.0.0-beta</Version>
  <AssemblyVersion>4.0.0.0</AssemblyVersion>
  <FileVersion>4.0.0.0</FileVersion>
  ```

- [ ] **CHANGELOG.md** - Move "Unreleased" section to "## [4.0.0-beta] - 2025-MM-DD"

- [ ] **Any other documentation** that hardcodes version numbers

### Phase 3: Documentation Updates 📚

- [ ] **CHANGELOG.md** - Finalize entry with:
  - [ ] Clear breaking change warning at top
  - [ ] What changed
  - [ ] Why it changed
  - [ ] Migration guide
  - [ ] New features/examples list
  - [ ] Date of release

- [ ] **README.md** - Update:
  - [ ] Version badge
  - [ ] Installation examples
  - [ ] Quick start code (verify still accurate)

- [ ] **Create GitHub Release Notes** (draft):
  - [ ] Title: "v4.0.0-beta - Breaking Change: 64-bit RawInstrumentId"
  - [ ] Breaking change warning (bold, at top)
  - [ ] Migration guide (copy from CHANGELOG)
  - [ ] What's new section
  - [ ] List new examples

### Phase 4: NuGet Package Preparation 📦

- [ ] **Build Release configuration**
  ```bash
  dotnet build -c Release
  ```

- [ ] **Create NuGet package**
  ```bash
  dotnet pack src/Databento.Client/Databento.Client.csproj -c Release
  ```

- [ ] **Verify package contents**
  - [ ] Native libraries included (win-x64, linux-x64, osx-arm64)
  - [ ] Dependencies correct
  - [ ] Version number correct in .nuspec

- [ ] **Test package locally** (optional but recommended)
  ```bash
  # Install from local .nupkg to test project
  dotnet add package Databento.Client --source ./artifacts
  ```

### Phase 5: Git & GitHub 🔧

- [ ] **Commit all changes**
  ```bash
  git add .
  git commit -m "chore: Release v4.0.0-beta - Breaking change: RawInstrumentId uint to ulong"
  ```

- [ ] **Create git tag**
  ```bash
  git tag -a v4.0.0-beta -m "Release v4.0.0-beta: Breaking change - RawInstrumentId uint to ulong"
  ```

- [ ] **Push to GitHub**
  ```bash
  git push origin master
  git push origin v4.0.0-beta
  ```

- [ ] **Create GitHub Release**
  - [ ] Use tag v4.0.0-beta
  - [ ] Title: "v4.0.0-beta - Breaking Change: 64-bit RawInstrumentId Support"
  - [ ] Body: Copy prepared release notes
  - [ ] Mark as "Pre-release" (beta)
  - [ ] Attach .nupkg file (optional)

### Phase 6: NuGet Publishing 🚀

- [ ] **Publish to NuGet.org**
  ```bash
  dotnet nuget push ./artifacts/Databento.Client.4.0.0-beta.nupkg \
    --api-key YOUR_API_KEY \
    --source https://api.nuget.org/v3/index.json
  ```

- [ ] **Verify package appears** on https://www.nuget.org/packages/Databento.Client

- [ ] **Test installation from NuGet**
  ```bash
  dotnet new console -n TestInstall
  cd TestInstall
  dotnet add package Databento.Client --version 4.0.0-beta --prerelease
  dotnet build
  ```

### Phase 7: Communication 📢

- [ ] **Update GitHub README** (if different from repo README)

- [ ] **Announce on GitHub Discussions** (if enabled)
  - Breaking change announcement
  - Migration guide
  - Link to release notes

- [ ] **Update any external documentation**
  - Project wiki (if exists)
  - Blog post (if applicable)

---

## 📝 CHANGELOG Entry Template

Use this for the final CHANGELOG.md entry:

```markdown
## [4.0.0-beta] - 2025-11-25

### 🚨 BREAKING CHANGES

**InstrumentDefMessage.RawInstrumentId changed from `uint` to `ulong`**

Some venues like Eurex (XEUR.EOBI) use 64-bit instrument IDs exceeding `uint.MaxValue` (4,294,967,295). Example: Eurex spread `0x010002B100000060` = 72,060,553,270,394,976.

**Migration (30 seconds):**
```csharp
// OLD (v3.0.29-beta and earlier)
uint rawId = instrumentDef.RawInstrumentId;

// NEW (v4.0.0-beta)
ulong rawId = instrumentDef.RawInstrumentId;
```

**Impact Areas:**
- Explicit `uint` type declarations → Change to `ulong`
- Database schemas (INT column) → Change to BIGINT
- Serialization/API contracts expecting 32-bit → Update to 64-bit

**Most code will work unchanged** due to implicit conversions. If you use `var` or don't use RawInstrumentId, no changes needed.

### Added

**New Examples:**
- `IntradayReplay2.Example` - Demonstrates LiveClient replay mode with StreamAsync()
- `Get_Most_Recent_Market_Open.Example` - Calculates and queries most recent market open
- `List_Available_Schemas.Example` - Demonstrates Historical::MetadataListSchemas

**Example Improvements:**
- `InstrumentDefinitionDecoder.Example` → Renamed to OHLCV Bar Decoder (Definition schema has no time-series data)
- Fixed TestsScratchpad.Internal to properly keep stream alive

### Changed

- **InstrumentDefMessage.RawInstrumentId**: `uint` → `ulong` to support 64-bit venue IDs
- **Record.ReadRawInstrumentId()**: Return type `uint` → `ulong`, simplified implementation

### Fixed

- Fixed streaming examples that exited immediately (missing StreamAsync loop or Task.Delay)
- Clarified difference between LiveBlockingClient (pull) vs LiveClient (push) in documentation

### Documentation

- Enhanced README.md with clear LiveClient vs LiveBlockingClient comparison
- Added comprehensive symbol mapping examples
- Improved replay mode documentation with practical examples
```

---

## 🎯 GitHub Release Notes Template

Use this when creating the GitHub release:

```markdown
# v4.0.0-beta - Breaking Change: 64-bit RawInstrumentId Support

## 🚨 BREAKING CHANGE - Action Required

`InstrumentDefMessage.RawInstrumentId` changed from `uint` (32-bit) to `ulong` (64-bit) to support venues with large instrument IDs.

**Quick Fix (30 seconds):**
```csharp
// Change this:
uint rawId = instrumentDef.RawInstrumentId;

// To this:
ulong rawId = instrumentDef.RawInstrumentId;
```

**Why?** Eurex (XEUR.EOBI) and other venues use 64-bit IDs like `72,060,553,270,394,976` that exceed `uint.MaxValue`.

**Who's Affected?**
- ✅ Code with explicit `uint` declarations
- ✅ Database INT columns storing RawInstrumentId
- ✅ Serialization code
- ❌ Code using `var` (type inference) - **no change needed**
- ❌ Code not using RawInstrumentId - **no change needed**

See full migration guide in CHANGELOG.md

---

## ✨ What's New

### New Examples
- `IntradayReplay2.Example` - Replay mode with LiveClient streaming
- `Get_Most_Recent_Market_Open.Example` - Market open time calculation
- `List_Available_Schemas.Example` - Schema discovery via MetadataListSchemas

### Improvements
- Renamed InstrumentDefinitionDecoder → OHLCV Bar Decoder (Definition schema fix)
- Fixed streaming examples that exited too early
- Enhanced documentation for LiveClient vs LiveBlockingClient

### Bug Fixes
- Fixed TestsScratchpad.Internal stream lifetime management
- Clarified push-based (streaming) vs pull-based (blocking) patterns

---

## 📦 Installation

```bash
dotnet add package Databento.Client --version 4.0.0-beta --prerelease
```

**NuGet**: https://www.nuget.org/packages/Databento.Client/4.0.0-beta

---

## 📚 Documentation

- [CHANGELOG.md](./CHANGELOG.md) - Full changelog with migration guide
- [README.md](./README.md) - Updated quick start examples
- [Examples](./examples/) - 30+ working examples

---

## ⚠️ Known Issues

None at this time. Please report issues at https://github.com/Alparse/databento-dotnet/issues

---

## 🙏 Acknowledgments

Built on [databento-cpp](https://github.com/databento/databento-cpp) v0.44.0
```

---

## 🔍 Testing Verification Steps

Before publishing, manually verify:

1. **Build Clean**
   ```bash
   git clean -fdx  # Remove all build artifacts
   dotnet build -c Release
   # Should succeed with 0 errors
   ```

2. **Run Examples**
   ```bash
   dotnet run --project examples/HistoricalData.Example
   dotnet run --project examples/LiveStreaming.Example  # Will need market hours or replay
   dotnet run --project examples/IntradayReplay2.Example
   dotnet run --project examples/Get_Most_Recent_Market_Open.Example
   ```

3. **Test Breaking Change**
   ```bash
   # Create test project
   mkdir test-v4
   cd test-v4
   dotnet new console
   # Add package reference manually to v4.0.0-beta
   # Write code using uint - verify it breaks
   # Write code using ulong - verify it works
   ```

4. **Verify XEUR.EOBI** (if you have access)
   ```bash
   # Run TestsScratchpad.Internal or similar
   # Should load 64-bit IDs without overflow
   ```

---

## 🚨 Rollback Plan

If critical issues discovered after release:

1. **Immediate**: Delist v4.0.0-beta from NuGet (mark as deprecated)
2. **Short-term**: Publish v3.0.30-beta with fixes (keep uint)
3. **Long-term**: Address issues and re-release v4.0.1-beta

**Not recommended**: Reverting is messy. Better to fix forward.

---

## 📊 Impact Assessment

### Low Impact (Most Users)
- US equities only → All IDs fit in uint anyway
- Using `var` → Automatic type inference handles it
- Not using RawInstrumentId → Zero impact

### Medium Impact
- Storing RawInstrumentId in database → Schema migration needed
- Serializing InstrumentDefMessage → Re-serialize needed

### High Impact
- Explicit uint declarations → Code changes required
- Third-party APIs expecting 32-bit → Contract breaking

**Overall Risk**: LOW-MEDIUM
**User Effort**: MINIMAL (most) to LOW (affected users)

---

## ✅ Sign-Off

- [ ] All checklist items completed
- [ ] Breaking change clearly communicated
- [ ] Migration guide tested and verified
- [ ] Examples run successfully
- [ ] Documentation updated
- [ ] Ready to tag and publish

**Prepared by**: Claude Code
**Date**: 2025-11-24
**Status**: READY FOR REVIEW

---

## 📞 Support

- **Issues**: https://github.com/Alparse/databento-dotnet/issues
- **Discussions**: https://github.com/Alparse/databento-dotnet/discussions
- **Databento Support**: https://databento.com/support
