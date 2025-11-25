# Plan: Change RawInstrumentId from uint to ulong

## Executive Summary

Change `InstrumentDefMessage.RawInstrumentId` from `uint` (32-bit) to `ulong` (64-bit) to support venues like Eurex (XEUR.EOBI) that use 64-bit instrument IDs exceeding uint.MaxValue (4,294,967,295).

**Root Cause**: Eurex encodes complex instruments (spreads) using 64-bit IDs like `0x010002B100000060` (72,060,553,270,394,976).

**Status**: This is a **BREAKING CHANGE** but necessary for correctness.

---

## Problem Statement

### Current Behavior
```
ERROR: System.OverflowException
RawInstrumentId value 72,060,553,270,394,976 exceeds uint.MaxValue (4,294,967,295)
```

### Data Analysis (XEUR.EOBI)
```
Future Spreads (complex instruments):
  0x010002B100000060 = 72,060,553,270,394,976  (spread ID 96)
  0x010002B10000005F = 72,060,553,270,394,975  (spread ID 95)
  0x010002B10000005E = 72,060,553,270,394,974  (spread ID 94)

Regular Futures (simple instruments):
  0x0000000000C55DCE = 12,934,606 (fits in uint)
  0x0000000000BCCA03 = 12,372,483 (fits in uint)
  0x0000000000B45D66 = 11,820,390 (fits in uint)
```

**Conclusion**: Data is valid, not corrupted. Eurex uses upper 32 bits for venue/type encoding.

---

## Changes Required

### 1. Core Library Changes

#### ✅ COMPLETED: InstrumentDefMessage.cs (line 93)
```csharp
// OLD (v3.0.29-beta and earlier):
public uint RawInstrumentId { get; set; }

// NEW (v3.0.30-beta):
public ulong RawInstrumentId { get; set; }
```

#### 🔄 IN PROGRESS: Record.cs ReadRawInstrumentId method (lines 845-867)

**Current (with debug code):**
```csharp
private static uint ReadRawInstrumentId(ReadOnlySpan<byte> bytes, int offset)
{
    ulong rawId64 = ReadUInt64(bytes, offset);

    // DEBUG: Log the value to verify it's legitimate
    Console.WriteLine($"[DEBUG] RawInstrumentId read: {rawId64:N0} (0x{rawId64:X16})");
    Console.WriteLine($"[DEBUG]   Upper 32 bits: {(rawId64 >> 32):N0} (0x{(rawId64 >> 32):X8})");
    Console.WriteLine($"[DEBUG]   Lower 32 bits: {(rawId64 & 0xFFFFFFFF):N0} (0x{(rawId64 & 0xFFFFFFFF):X8})");

    if (rawId64 > uint.MaxValue)
    {
        Console.WriteLine($"[DEBUG] Value exceeds uint.MaxValue - returning lower 32 bits as temporary workaround");
        return (uint)(rawId64 & 0xFFFFFFFF); // Temporary: use lower 32 bits
    }

    return (uint)rawId64;
}
```

**New (clean implementation):**
```csharp
/// <summary>
/// Reads RawInstrumentId from bytes as 64-bit value per DBN specification.
/// </summary>
private static ulong ReadRawInstrumentId(ReadOnlySpan<byte> bytes, int offset)
{
    return ReadUInt64(bytes, offset);
}
```

#### Record.cs line 446 (assignment)
- Already correct: `RawInstrumentId = ReadRawInstrumentId(bytes, 112),`
- No change needed (type inference will work with ulong return)

---

### 2. Example/Test Updates

#### ✅ ALREADY CORRECT: InstrumentDefTest.v4
- Lines 313-322: Already expects `ulong`
- Line 395: Display code works with ulong
- **Action**: None required

#### ❓ TestsScratchpad.Internal
- Currently queries XEUR.EOBI data
- Will be used to verify the fix works
- **Action**: Test after changes

#### ❓ Other Examples
- Need to verify if any other examples directly use `RawInstrumentId`
- **Action**: Search and verify after core changes

---

### 3. Documentation Updates

#### CHANGELOG.md
Add entry under "Unreleased" or new version:
```markdown
### Changed (BREAKING)
- **InstrumentDefMessage.RawInstrumentId** changed from `uint` to `ulong` to support venues with 64-bit instrument IDs (e.g., Eurex XEUR.EOBI). [Issue: RawInstrumentId overflow for Eurex instruments]

### Migration Guide
If you use `RawInstrumentId`:
```csharp
// OLD (v3.0.29-beta and earlier)
uint rawId = instrumentDef.RawInstrumentId;

// NEW (v3.0.30-beta and later)
ulong rawId = instrumentDef.RawInstrumentId;
```

Most code will work unchanged due to implicit conversions, but if you have:
- Explicit `uint` type declarations
- Serialization/deserialization code
- API contracts expecting 32-bit values

...you will need to update to `ulong`.
```

#### API_REFERENCE.md (if exists)
- Update any references to RawInstrumentId type
- Add note about 64-bit support for Eurex/other venues

#### README.md (if InstrumentDefMessage is documented there)
- Update type information

---

### 4. Breaking Change Analysis

#### Who is affected?
Users who:
1. **Directly access** `instrumentDef.RawInstrumentId` with explicit `uint` type
2. **Serialize/deserialize** InstrumentDefMessage (binary/JSON)
3. **Store** RawInstrumentId in databases with 32-bit integer columns
4. **Pass** RawInstrumentId to APIs expecting 32-bit integers

#### Who is NOT affected?
Users who:
1. Use `var rawId = instrumentDef.RawInstrumentId` (type inference)
2. Don't use RawInstrumentId at all (most users)
3. Use implicit conversions (ulong → long, etc.)

#### Migration Difficulty
- **Easy**: Change `uint` to `ulong` in declarations
- **Medium**: Update database schemas (INT → BIGINT)
- **Hard**: Third-party integrations with fixed schemas

---

## Implementation Steps

### Phase 1: Core Changes ⏳
1. ✅ Update `InstrumentDefMessage.RawInstrumentId` to `ulong` (DONE)
2. 🔄 Update `ReadRawInstrumentId()` method:
   - Change return type from `uint` to `ulong`
   - Remove overflow check and exception
   - Remove debug Console.WriteLine statements
   - Simplify to single line: `return ReadUInt64(bytes, offset);`
3. ⏳ Update XML documentation comment for method

### Phase 2: Verification ⏳
1. ⏳ Build solution (`dotnet build`)
2. ⏳ Run `TestsScratchpad.Internal` to verify XEUR.EOBI data loads without error
3. ⏳ Run `InstrumentDefTest.v4` to verify ulong assertions pass
4. ⏳ Search for any other RawInstrumentId usages in examples

### Phase 3: Documentation ⏳
1. ⏳ Update CHANGELOG.md with breaking change notice
2. ⏳ Add migration guide snippet
3. ⏳ Update any API documentation referencing RawInstrumentId

### Phase 4: Testing ⏳
1. ⏳ Test with XEUR.EOBI data (existing TestsScratchpad.Internal)
2. ⏳ Test with regular uint-range data (US equities, etc.)
3. ⏳ Verify all examples still build
4. ⏳ Verify InstrumentDefTest.v4 passes

### Phase 5: Release Prep ⏳
1. ⏳ Commit changes with descriptive message
2. ⏳ Tag as breaking change release (v3.0.30-beta or v4.0.0 depending on versioning strategy)
3. ⏳ Update release notes

---

## Success Criteria

✅ **Must Have:**
1. `TestsScratchpad.Internal` runs without overflow exception
2. All XEUR.EOBI instrument definitions load successfully
3. 64-bit RawInstrumentId values display correctly
4. Solution builds without errors or warnings
5. `InstrumentDefTest.v4` passes all assertions

✅ **Should Have:**
1. CHANGELOG.md documents breaking change
2. Migration guide available for users
3. All examples still compile

✅ **Nice to Have:**
1. API_REFERENCE.md updated (if applicable)
2. Example showing how to handle large RawInstrumentId values
3. Performance verification (should be identical)

---

## Risks and Mitigations

### Risk 1: Breaking existing code
**Impact**: HIGH
**Likelihood**: MEDIUM
**Mitigation**:
- Document breaking change prominently in CHANGELOG
- Provide clear migration guide
- Consider if this should be v4.0.0 (major version bump)

### Risk 2: Database schema incompatibility
**Impact**: MEDIUM
**Likelihood**: LOW (most users likely don't store RawInstrumentId)
**Mitigation**:
- Document need to change INT → BIGINT in databases
- Provide SQL migration examples if needed

### Risk 3: Serialization issues
**Impact**: MEDIUM
**Likelihood**: LOW
**Mitigation**:
- Test JSON serialization/deserialization
- Note in docs that binary serialization format changed

### Risk 4: Performance impact
**Impact**: LOW
**Likelihood**: VERY LOW (64-bit operations are native on modern CPUs)
**Mitigation**:
- None needed; performance should be identical

---

## Rollback Plan

If critical issues discovered after release:

1. **Quick fix option**: Add configuration flag to use uint/ulong
2. **Revert option**: Restore uint, add venue-specific handling
3. **Alternative**: Provide separate property `RawInstrumentId64` keeping `RawInstrumentId` as uint (deprecated)

**Recommended**: Don't rollback. This is the correct fix. Users must update.

---

## Questions for Review

1. **Versioning**: Should this be v3.0.30-beta or v4.0.0?
   - v3.x.x: Breaking change in beta (acceptable)
   - v4.0.0: Major version for breaking change (preferred)

2. **Backwards compat**: Should we provide any compatibility shims?
   - Recommend: No. Clean break is better.

3. **Documentation**: Where else should this be documented?
   - Check: README.md, API_REFERENCE.md, GitHub issues

4. **Testing**: What additional test coverage is needed?
   - Consider: Integration test for XEUR.EOBI venue
   - Consider: Unit test for 64-bit value handling

---

## Estimated Effort

- **Code changes**: 15 minutes (simple)
- **Testing**: 30 minutes (verify multiple scenarios)
- **Documentation**: 30 minutes (CHANGELOG, migration guide)
- **Total**: ~1.5 hours

---

## Dependencies

None. This is a self-contained change.

---

## Sign-off

- [ ] Plan reviewed and approved
- [ ] Breaking change severity acknowledged
- [ ] Documentation requirements understood
- [ ] Testing plan agreed upon
- [ ] Ready to proceed with implementation

---

**Created**: 2025-11-24
**Status**: PENDING REVIEW
**Assigned**: Claude Code
**Priority**: HIGH (blocking XEUR.EOBI data access)
