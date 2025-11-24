# Issue #4 Fix Plan: InstrumentDefMessage.InstrumentClass Always 0

## Problem Summary

When querying instrument definitions from `schema=Definition` on `GLBX.MDP3` dataset, the `InstrumentDefMessage.InstrumentClass` property is always `0`, which doesn't map to any value in the `InstrumentClass` enum.

```csharp
// User's code
await foreach (var record in client.GetRangeAsync(
    dataset: "GLBX.MDP3",
    schema: Schema.Definition,
    symbols: new[] { "ALL_SYMBOLS" },
    startTime: start,
    endTime: end))
{
    if (record is InstrumentDefMessage def)
    {
        Console.WriteLine(def.InstrumentClass); // Always prints 0
    }
}
```

## Root Cause Analysis

### Current Implementation

**File**: `src/Databento.Client/Models/Record.cs:416-484`

The `DeserializeInstrumentDefMsg()` method deserializes the 520-byte `InstrumentDefMsg` structure. On **line 450**:

```csharp
InstrumentClass instrumentClass = (InstrumentClass)bytes[319];
```

This reads the instrument class from byte offset 319.

### The InstrumentClass Enum

**File**: `src/Databento.Client/Models/Enums.cs:110-122`

```csharp
public enum InstrumentClass : byte
{
    Bond = (byte)'B',           // 66
    Call = (byte)'C',           // 67
    Future = (byte)'F',         // 70
    Stock = (byte)'K',          // 75
    MixedSpread = (byte)'M',    // 77
    Put = (byte)'P',            // 80
    FutureSpread = (byte)'S',   // 83
    OptionSpread = (byte)'T',   // 84
    FxSpot = (byte)'X',         // 88
    CommoditySpot = (byte)'Y'   // 89
}
```

**Problem**: The enum uses ASCII character codes ('B', 'C', 'F', etc.) but has **NO value for 0**.

### Potential Causes

1. **Incorrect Byte Offset** - Offset 319 may be wrong for the `instrument_class` field
2. **Incomplete Deserialization** - The comment on line 425 admits this is "simplified deserialization of the most important fields"
3. **Data Issue** - The actual data from Databento may have 0 in that field (unlikely)
4. **Missing DBN Spec Alignment** - The deserialization may not match the official DBN v2 specification

## Investigation Steps

### Step 1: Verify DBN Specification

**Action**: Check the official Databento DBN v2 specification for the `InstrumentDefMsg` layout.

**Resources**:
- Check `build/native/_deps/databento-cpp-src/` for C++ implementation
- Review Databento's official documentation at https://docs.databento.com/
- Compare with Python implementation (databento-python)

**Expected Outcome**: Confirm the exact byte offset for `instrument_class` field.

### Step 2: Examine databento-cpp Source

**Action**: Look at the C++ library's `InstrumentDefMsg` structure definition.

**File to Check**: `build/native/_deps/databento-cpp-src/include/databento/record.hpp` (or similar)

**Expected Outcome**: Find the correct struct layout with field offsets.

### Step 3: Test with Sample Data

**Action**: Write a test program to:
1. Request instrument definitions from GLBX.MDP3
2. Dump the raw bytes at offset 319 and surrounding bytes
3. Inspect what actual values are present

**Expected Outcome**: Determine if the data is truly 0 or if we're reading from wrong offset.

### Step 4: Compare Other Fields

**Action**: Check if other enum fields are being read correctly:
- `MatchAlgorithm` at offset 328 (line 452)
- Other byte/char fields

**Expected Outcome**: Confirm if the issue is specific to `InstrumentClass` or affects other fields too.

## Proposed Solutions

### Solution A: Verify and Fix Byte Offset (RECOMMENDED)

**If Investigation Shows**: Offset 319 is incorrect.

**Changes Required**:
1. Update `Record.cs:450` with correct offset
2. Add comment documenting the correct DBN v2 layout
3. Add unit tests to verify correct deserialization

**Pros**:
- Fixes the root cause
- Aligns with DBN specification
- No breaking changes to API

**Cons**:
- Requires careful validation of DBN spec

**Implementation**:
```csharp
// Record.cs:450 (example - offset TBD from spec)
// Correct offset based on DBN v2 spec
InstrumentClass instrumentClass = (InstrumentClass)bytes[XXX]; // Update offset
```

### Solution B: Add Unknown/Undefined Value to Enum

**If Investigation Shows**: Data is legitimately 0 for some instruments.

**Changes Required**:
1. Add `Unknown = 0` to `InstrumentClass` enum
2. Document when this value appears
3. Update any switch statements handling `InstrumentClass`

**Pros**:
- Gracefully handles undefined values
- No data loss
- Simple implementation

**Cons**:
- Doesn't fix underlying issue if offset is wrong
- May mask real problems

**Implementation**:
```csharp
// Enums.cs:110
public enum InstrumentClass : byte
{
    Unknown = 0,               // Undefined or not applicable
    Bond = (byte)'B',
    Call = (byte)'C',
    // ... rest unchanged
}
```

### Solution C: Complete the Deserialization (COMPREHENSIVE)

**If Investigation Shows**: Many fields are missing/incorrect.

**Changes Required**:
1. Fully deserialize all 520 bytes of `InstrumentDefMsg`
2. Map ALL fields according to DBN v2 spec
3. Add comprehensive field offset documentation
4. Create extensive unit tests

**Pros**:
- Most thorough solution
- Ensures all fields are correct
- Future-proofs the implementation

**Cons**:
- Most time-consuming
- Risk of introducing new bugs if done incorrectly
- Requires complete DBN spec understanding

**Implementation**:
```csharp
// Record.cs:416-484 - Complete rewrite with all fields and correct offsets
private static InstrumentDefMessage DeserializeInstrumentDefMsg(...)
{
    // Full implementation with:
    // - All 520 bytes documented
    // - Correct offsets verified against DBN v2 spec
    // - All string fields
    // - All numeric fields
    // - All enum fields
}
```

### Solution D: Hybrid Approach (RECOMMENDED)

**Combine Solutions A + B**:

1. **Verify and fix the byte offset** (Solution A)
2. **Add `Unknown = 0` to enum** (Solution B) as a safety net
3. **Document known limitations** in XML comments

**Pros**:
- Fixes root cause
- Provides graceful degradation
- Doesn't break existing code
- Minimal risk

**Cons**:
- Slightly more work than Solution A alone

## Recommended Plan

### Phase 1: Investigation (1-2 hours)
1. ✅ Read DBN v2 specification from Databento docs
2. ✅ Examine databento-cpp source code for correct layout
3. ✅ Write diagnostic test to dump raw bytes
4. ✅ Verify correct offset for `instrument_class`

### Phase 2: Implementation (2-3 hours)
1. ✅ Add `Unknown = 0` to `InstrumentClass` enum (safety net)
2. ✅ Update offset in `Record.cs:450` if incorrect
3. ✅ Add XML documentation explaining the field
4. ✅ Add detailed byte offset comments in deserialization code

### Phase 3: Testing (1-2 hours)
1. ✅ Write unit test with known GLBX.MDP3 instrument definition data
2. ✅ Test with multiple instrument types (futures, options, etc.)
3. ✅ Verify other enum fields still work correctly
4. ✅ Run integration test with live Historical API

### Phase 4: Validation & Release
1. ✅ User acceptance testing with issue reporter
2. ✅ Update CHANGELOG.md
3. ✅ Release as v3.0.27-beta (or next version)

## Testing Strategy

### Unit Tests
```csharp
[Fact]
public void InstrumentDefMessage_DeserializesInstrumentClass_Correctly()
{
    // Arrange: Raw bytes with known instrument_class value
    byte[] rawBytes = new byte[520];
    rawBytes[XXX] = (byte)'F'; // Future at correct offset

    // Act
    var record = Record.FromBytes(rawBytes, 0x13);
    var def = record as InstrumentDefMessage;

    // Assert
    Assert.NotNull(def);
    Assert.Equal(InstrumentClass.Future, def.InstrumentClass);
}

[Fact]
public void InstrumentDefMessage_HandlesUnknownInstrumentClass()
{
    // Arrange: Raw bytes with 0 for instrument_class
    byte[] rawBytes = new byte[520];
    rawBytes[XXX] = 0; // Unknown/undefined

    // Act
    var record = Record.FromBytes(rawBytes, 0x13);
    var def = record as InstrumentDefMessage;

    // Assert
    Assert.NotNull(def);
    Assert.Equal(InstrumentClass.Unknown, def.InstrumentClass);
}
```

### Integration Test
```csharp
[Fact]
public async Task GetRangeAsync_GLBX_MDP3_Definition_ReturnsNonZeroInstrumentClass()
{
    // Arrange
    var client = new HistoricalClient(apiKey);

    // Act
    var records = await client.GetRangeAsync(
        dataset: "GLBX.MDP3",
        schema: Schema.Definition,
        symbols: new[] { "ES.FUT" }, // S&P 500 future
        startTime: DateTimeOffset.UtcNow.AddDays(-1),
        endTime: DateTimeOffset.UtcNow)
        .Take(10)
        .ToListAsync();

    // Assert
    var defs = records.OfType<InstrumentDefMessage>().ToList();
    Assert.NotEmpty(defs);
    Assert.All(defs, def => Assert.NotEqual(0, (byte)def.InstrumentClass));
}
```

## Success Criteria

1. ✅ `InstrumentClass` is populated with correct values from API
2. ✅ User can filter futures using `def.InstrumentClass == InstrumentClass.Future`
3. ✅ No breaking changes to existing API
4. ✅ All unit tests pass
5. ✅ Integration test with live API passes
6. ✅ Issue reporter confirms fix works

## Timeline

- **Investigation**: 1-2 hours
- **Implementation**: 2-3 hours
- **Testing**: 1-2 hours
- **Review & Release**: 1 hour

**Total Estimate**: 5-8 hours

## Risk Assessment

### Low Risk
- Adding `Unknown = 0` to enum
- Updating single byte offset
- Adding XML documentation

### Medium Risk
- Changing byte offset (could break if wrong)
- Extensive refactoring of deserialization

### Mitigation
- Thorough testing before release
- Beta release tag for validation
- Easy rollback via git if issues found

## Next Steps

1. **User Approval**: Review this plan
2. **Begin Investigation**: Follow Phase 1 steps
3. **Report Findings**: Document actual byte offset
4. **Implement Fix**: Follow recommended solution (D)
5. **Test & Validate**: Run all tests
6. **Release**: Publish as next beta version
