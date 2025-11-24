# Issue #4 Fix Plan: InstrumentDefMessage.InstrumentClass Always 0 (REVISED)

**Issue**: https://github.com/Alparse/databento-dotnet/issues/4

## Executive Summary

**CRITICAL BUG DISCOVERED**: The C# `InstrumentDefMessage` deserialization in `Record.cs` uses **completely incorrect byte offsets** that don't match the actual DBN specification. This affects not just `InstrumentClass`, but potentially **ALL fields** in the struct.

### Root Cause

**File**: `src/Databento.Client/Models/Record.cs:416-484`

The `DeserializeInstrumentDefMsg()` method reads:
- `InstrumentClass` from byte offset **319** (line 450)
- `StrikePrice` from offset **320** (line 451)
- String fields from various incorrect offsets

**Actual DBN Specification**:
- `InstrumentClass` is at byte offset **487** (not 319)
- The struct is 520 bytes total with completely different field layout
- All string fields are at different offsets than the C# code assumes

**Evidence**:
1. Official DBN repo: https://github.com/databento/dbn
2. Rust documentation: https://docs.rs/dbn/latest/dbn/record/struct.InstrumentDefMsg.html
3. Local C++ source: `build/native/_deps/databento-cpp-src/include/databento/record.hpp`

## Correct DBN InstrumentDefMsg Layout

Based on the official DBN specification from databento-cpp:

```cpp
struct InstrumentDefMsg {
  RecordHeader hd;                            // 16 bytes  (offset 0)
  UnixNanos ts_recv;                          // 8 bytes   (offset 16)
  std::int64_t min_price_increment;           // 8 bytes   (offset 24)
  std::int64_t display_factor;                // 8 bytes   (offset 32)
  UnixNanos expiration;                       // 8 bytes   (offset 40)
  UnixNanos activation;                       // 8 bytes   (offset 48)
  std::int64_t high_limit_price;              // 8 bytes   (offset 56)
  std::int64_t low_limit_price;               // 8 bytes   (offset 64)
  std::int64_t max_price_variation;           // 8 bytes   (offset 72)
  std::int64_t unit_of_measure_qty;           // 8 bytes   (offset 80)  ← MISSING in C#!
  std::int64_t min_price_increment_amount;    // 8 bytes   (offset 88)
  std::int64_t price_ratio;                   // 8 bytes   (offset 96)
  std::int64_t strike_price;                  // 8 bytes   (offset 104) ← C# reads from 320!
  std::uint64_t raw_instrument_id;            // 8 bytes   (offset 112)
  std::int64_t leg_price;                     // 8 bytes   (offset 120) ← NEW field
  std::int64_t leg_delta;                     // 8 bytes   (offset 128) ← NEW field
  std::int32_t inst_attrib_value;             // 4 bytes   (offset 136)
  std::uint32_t underlying_id;                // 4 bytes   (offset 140)
  std::int32_t market_depth_implied;          // 4 bytes   (offset 144)
  std::int32_t market_depth;                  // 4 bytes   (offset 148)
  std::uint32_t market_segment_id;            // 4 bytes   (offset 152)
  std::uint32_t max_trade_vol;                // 4 bytes   (offset 156)
  std::int32_t min_lot_size;                  // 4 bytes   (offset 160)
  std::int32_t min_lot_size_block;            // 4 bytes   (offset 164)
  std::int32_t min_lot_size_round_lot;        // 4 bytes   (offset 168)
  std::uint32_t min_trade_vol;                // 4 bytes   (offset 172)
  std::int32_t contract_multiplier;           // 4 bytes   (offset 176)
  std::int32_t decay_quantity;                // 4 bytes   (offset 180)
  std::int32_t original_contract_size;        // 4 bytes   (offset 184)
  std::uint32_t leg_instrument_id;            // 4 bytes   (offset 188) ← NEW field
  std::int32_t leg_ratio_price_numerator;     // 4 bytes   (offset 192) ← NEW field
  std::int32_t leg_ratio_price_denominator;   // 4 bytes   (offset 196) ← NEW field
  std::int32_t leg_ratio_qty_numerator;       // 4 bytes   (offset 200) ← NEW field
  std::int32_t leg_ratio_qty_denominator;     // 4 bytes   (offset 204) ← NEW field
  std::uint32_t leg_underlying_id;            // 4 bytes   (offset 208) ← NEW field
  std::int16_t appl_id;                       // 2 bytes   (offset 212)
  std::uint16_t maturity_year;                // 2 bytes   (offset 214)
  std::uint16_t decay_start_date;             // 2 bytes   (offset 216)
  std::uint16_t channel_id;                   // 2 bytes   (offset 218)
  std::uint16_t leg_count;                    // 2 bytes   (offset 220) ← NEW field
  std::uint16_t leg_index;                    // 2 bytes   (offset 222) ← NEW field
  std::array<char, 4> currency;               // 4 bytes   (offset 224) ← C# reads from 178!
  std::array<char, 4> settl_currency;         // 4 bytes   (offset 228) ← C# reads from 183!
  std::array<char, 6> secsubtype;             // 6 bytes   (offset 232) ← C# reads from 188!
  std::array<char, 71> raw_symbol;            // 71 bytes  (offset 238) ← C# reads 22 bytes from 194!
  std::array<char, 21> group;                 // 21 bytes  (offset 309) ← C# reads from 216!
  std::array<char, 5> exchange;               // 5 bytes   (offset 330) ← C# reads from 237!
  std::array<char, 11> asset;                 // 11 bytes  (offset 335) ← C# reads 7 bytes from 242!
  std::array<char, 7> cfi;                    // 7 bytes   (offset 346) ← C# reads from 249!
  std::array<char, 7> security_type;          // 7 bytes   (offset 353) ← C# reads from 256!
  std::array<char, 31> unit_of_measure;       // 31 bytes  (offset 360) ← C# reads from 263!
  std::array<char, 21> underlying;            // 21 bytes  (offset 391) ← C# reads from 294!
  std::array<char, 4> strike_price_currency;  // 4 bytes   (offset 412) ← MISSING in C#!
  std::array<char, 71> leg_raw_symbol;        // 71 bytes  (offset 416) ← MISSING in C#!
  InstrumentClass instrument_class;           // 1 byte    (offset 487) ← C# reads from 319!
  MatchAlgorithm match_algorithm;             // 1 byte    (offset 488) ← C# reads from 328!
  std::uint8_t main_fraction;                 // 1 byte    (offset 489) ← MISSING in C#!
  std::uint8_t price_display_format;          // 1 byte    (offset 490) ← MISSING in C#!
  std::uint8_t sub_fraction;                  // 1 byte    (offset 491) ← MISSING in C#!
  std::uint8_t underlying_product;            // 1 byte    (offset 492) ← MISSING in C#!
  SecurityUpdateAction security_update_action;// 1 byte    (offset 493) ← MISSING in C#!
  std::uint8_t maturity_month;                // 1 byte    (offset 494) ← MISSING in C#!
  std::uint8_t maturity_day;                  // 1 byte    (offset 495) ← MISSING in C#!
  std::uint8_t maturity_week;                 // 1 byte    (offset 496) ← MISSING in C#!
  UserDefinedInstrument user_defined_instrument; // 1 byte (offset 497) ← MISSING in C#!
  std::int8_t contract_multiplier_unit;       // 1 byte    (offset 498) ← MISSING in C#!
  std::int8_t flow_schedule_type;             // 1 byte    (offset 499) ← MISSING in C#!
  std::uint8_t tick_rule;                     // 1 byte    (offset 500) ← MISSING in C#!
  InstrumentClass leg_instrument_class;       // 1 byte    (offset 501) ← MISSING in C#!
  Side leg_side;                              // 1 byte    (offset 502) ← MISSING in C#!
  std::array<std::byte, 17> _reserved;        // 17 bytes  (offset 503-519)
};
static_assert(sizeof(InstrumentDefMsg) == 520);
```

### Current C# Implementation Offsets (WRONG)

```csharp
// Record.cs:427-482
long tsRecv = ReadInt64(bytes, 16);               // ✅ CORRECT
long minPriceIncrement = ReadInt64(bytes, 24);    // ✅ CORRECT
long displayFactor = ReadInt64(bytes, 32);        // ✅ CORRECT
long expiration = ReadInt64(bytes, 40);           // ✅ CORRECT
long activation = ReadInt64(bytes, 48);           // ✅ CORRECT
long highLimitPrice = ReadInt64(bytes, 56);       // ✅ CORRECT
long lowLimitPrice = ReadInt64(bytes, 64);        // ✅ CORRECT
long maxPriceVariation = ReadInt64(bytes, 72);    // ✅ CORRECT
long tradingReferencePrice = ReadInt64(bytes, 80); // ❌ WRONG - should be unit_of_measure_qty!

// MISSING: 8 fields (unit_of_measure_qty through leg_delta)
// MISSING: 6 leg-related fields (leg_instrument_id through leg_underlying_id)
// MISSING: 2 fields (leg_count, leg_index)

string currency = ReadCString(bytes, 178, 5);      // ❌ WRONG - should be 224, length 4
string settlCurrency = ReadCString(bytes, 183, 5); // ❌ WRONG - should be 228, length 4
string secSubType = ReadCString(bytes, 188, 6);    // ❌ WRONG - should be 232, length 6
string rawSymbol = ReadCString(bytes, 194, 22);    // ❌ WRONG - should be 238, length 71!
string group = ReadCString(bytes, 216, 21);        // ❌ WRONG - should be 309, length 21
string exchange = ReadCString(bytes, 237, 5);      // ❌ WRONG - should be 330, length 5
string asset = ReadCString(bytes, 242, 7);         // ❌ WRONG - should be 335, length 11!
string cfi = ReadCString(bytes, 249, 7);           // ❌ WRONG - should be 346, length 7
string securityType = ReadCString(bytes, 256, 7);  // ❌ WRONG - should be 353, length 7
string unitOfMeasure = ReadCString(bytes, 263, 31);// ❌ WRONG - should be 360, length 31
string underlying = ReadCString(bytes, 294, 21);   // ❌ WRONG - should be 391, length 21

// MISSING: strike_price_currency (offset 412)
// MISSING: leg_raw_symbol (offset 416)

InstrumentClass instrumentClass = (InstrumentClass)bytes[319]; // ❌ WRONG - should be 487!
long strikePrice = ReadInt64(bytes, 320);          // ❌ WRONG - should be 104!
MatchAlgorithm matchAlgorithm = (MatchAlgorithm)bytes[328];   // ❌ WRONG - should be 488!

// MISSING: 12+ additional fields
```

## Impact Assessment

### Severity: **CRITICAL** 🔴

**Affected:**
1. ✅ `InstrumentClass` - **Always 0** (reading wrong offset)
2. ⚠️ `StrikePrice` - **Likely incorrect** (reading from offset 320 instead of 104)
3. ⚠️ `MatchAlgorithm` - **Likely incorrect** (reading from offset 328 instead of 488)
4. ⚠️ **All string fields** - Reading from wrong offsets with wrong lengths
5. ❌ **20+ fields completely missing** (leg fields, multi-leg strategy support, etc.)

**Not Affected:**
- First 8 numeric fields (ts_recv through max_price_variation) - these are correct
- RType, PublisherId, InstrumentId, Timestamp (from header) - correct

### Why This Wasn't Caught Earlier

1. **Partial correctness**: First ~80 bytes are correct, so basic fields work
2. **String leniency**: C strings with null terminators are forgiving of length mismatches
3. **Limited testing**: Most users don't check `InstrumentClass` or other enum fields
4. **Schema version confusion**: The C# code may have been written for an older DBN version (v1?)

## Recommended Solution: Complete Rewrite

### Phase 1: Complete Field Mapping (REQUIRED)

**Update `InstrumentDefMessage.cs`** to include ALL fields from DBN spec:

```csharp
public class InstrumentDefMessage : Record
{
    // Existing fields (keep)
    public long TsRecv { get; set; }
    public long MinPriceIncrement { get; set; }
    public long DisplayFactor { get; set; }
    public long Expiration { get; set; }
    public long Activation { get; set; }
    public long HighLimitPrice { get; set; }
    public long LowLimitPrice { get; set; }
    public long MaxPriceVariation { get; set; }
    public long TradingReferencePrice { get; set; } // ← RENAME to UnitOfMeasureQty

    // NEW fields (add)
    public long UnitOfMeasureQty { get; set; }
    public long MinPriceIncrementAmount { get; set; }
    public long PriceRatio { get; set; }
    public long StrikePrice { get; set; }
    public ulong RawInstrumentId { get; set; }
    public long LegPrice { get; set; }            // ← NEW
    public long LegDelta { get; set; }            // ← NEW
    public int InstAttribValue { get; set; }
    public uint UnderlyingId { get; set; }
    public int MarketDepthImplied { get; set; }
    public int MarketDepth { get; set; }
    public uint MarketSegmentId { get; set; }
    public uint MaxTradeVol { get; set; }
    public int MinLotSize { get; set; }
    public int MinLotSizeBlock { get; set; }
    public int MinLotSizeRoundLot { get; set; }
    public uint MinTradeVol { get; set; }
    public int ContractMultiplier { get; set; }
    public int DecayQuantity { get; set; }
    public int OriginalContractSize { get; set; }
    public uint LegInstrumentId { get; set; }     // ← NEW
    public int LegRatioPriceNumerator { get; set; }   // ← NEW
    public int LegRatioPriceDenominator { get; set; } // ← NEW
    public int LegRatioQtyNumerator { get; set; }     // ← NEW
    public int LegRatioQtyDenominator { get; set; }   // ← NEW
    public uint LegUnderlyingId { get; set; }     // ← NEW
    public short ApplId { get; set; }
    public ushort MaturityYear { get; set; }
    public ushort DecayStartDate { get; set; }
    public ushort ChannelId { get; set; }
    public ushort LegCount { get; set; }          // ← NEW
    public ushort LegIndex { get; set; }          // ← NEW

    // String fields (existing)
    public string Currency { get; set; } = string.Empty;
    public string SettlCurrency { get; set; } = string.Empty;
    public string SecSubType { get; set; } = string.Empty;
    public string RawSymbol { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string Asset { get; set; } = string.Empty;
    public string Cfi { get; set; } = string.Empty;
    public string SecurityType { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string Underlying { get; set; } = string.Empty;
    public string StrikePriceCurrency { get; set; } = string.Empty; // ← NEW
    public string LegRawSymbol { get; set; } = string.Empty;         // ← NEW

    // Enum fields (existing)
    public InstrumentClass InstrumentClass { get; set; }
    public MatchAlgorithm MatchAlgorithm { get; set; }
    public SecurityUpdateAction SecurityUpdateAction { get; set; }
    public UserDefinedInstrument UserDefinedInstrument { get; set; }

    // NEW byte fields
    public byte MdSecurityTradingStatus { get; set; }
    public byte MainFraction { get; set; }             // ← NEW
    public byte PriceDisplayFormat { get; set; }       // ← NEW
    public byte SettlPriceType { get; set; }
    public byte SubFraction { get; set; }              // ← NEW
    public byte UnderlyingProduct { get; set; }        // ← NEW
    public byte MaturityMonth { get; set; }
    public byte MaturityDay { get; set; }
    public byte MaturityWeek { get; set; }
    public sbyte ContractMultiplierUnit { get; set; }
    public sbyte FlowScheduleType { get; set; }
    public byte TickRule { get; set; }                 // ← NEW
    public InstrumentClass LegInstrumentClass { get; set; } // ← NEW
    public Side LegSide { get; set; }                  // ← NEW
}
```

### Phase 2: Fix Deserialization (REQUIRED)

**Completely rewrite `Record.cs:416-484`** with correct offsets:

```csharp
private static InstrumentDefMessage DeserializeInstrumentDefMsg(
    ReadOnlySpan<byte> bytes, byte rtype,
    ushort publisherId, uint instrumentId, long tsEvent)
{
    const int ExpectedSize = 520;
    if (bytes.Length != ExpectedSize)
        throw new ArgumentException($"Invalid InstrumentDefMsg size: expected {ExpectedSize}, got {bytes.Length}");

    return new InstrumentDefMessage
    {
        RType = rtype,
        PublisherId = publisherId,
        InstrumentId = instrumentId,
        TimestampNs = tsEvent,

        // Numeric fields (offsets 16-223)
        TsRecv = ReadInt64(bytes, 16),
        MinPriceIncrement = ReadInt64(bytes, 24),
        DisplayFactor = ReadInt64(bytes, 32),
        Expiration = ReadInt64(bytes, 40),
        Activation = ReadInt64(bytes, 48),
        HighLimitPrice = ReadInt64(bytes, 56),
        LowLimitPrice = ReadInt64(bytes, 64),
        MaxPriceVariation = ReadInt64(bytes, 72),
        UnitOfMeasureQty = ReadInt64(bytes, 80),
        MinPriceIncrementAmount = ReadInt64(bytes, 88),
        PriceRatio = ReadInt64(bytes, 96),
        StrikePrice = ReadInt64(bytes, 104),           // ← FIX: was 320
        RawInstrumentId = ReadUInt64(bytes, 112),
        LegPrice = ReadInt64(bytes, 120),              // ← NEW
        LegDelta = ReadInt64(bytes, 128),              // ← NEW
        InstAttribValue = ReadInt32(bytes, 136),
        UnderlyingId = ReadUInt32(bytes, 140),
        MarketDepthImplied = ReadInt32(bytes, 144),
        MarketDepth = ReadInt32(bytes, 148),
        MarketSegmentId = ReadUInt32(bytes, 152),
        MaxTradeVol = ReadUInt32(bytes, 156),
        MinLotSize = ReadInt32(bytes, 160),
        MinLotSizeBlock = ReadInt32(bytes, 164),
        MinLotSizeRoundLot = ReadInt32(bytes, 168),
        MinTradeVol = ReadUInt32(bytes, 172),
        ContractMultiplier = ReadInt32(bytes, 176),
        DecayQuantity = ReadInt32(bytes, 180),
        OriginalContractSize = ReadInt32(bytes, 184),
        LegInstrumentId = ReadUInt32(bytes, 188),      // ← NEW
        LegRatioPriceNumerator = ReadInt32(bytes, 192),   // ← NEW
        LegRatioPriceDenominator = ReadInt32(bytes, 196), // ← NEW
        LegRatioQtyNumerator = ReadInt32(bytes, 200),     // ← NEW
        LegRatioQtyDenominator = ReadInt32(bytes, 204),   // ← NEW
        LegUnderlyingId = ReadUInt32(bytes, 208),      // ← NEW
        ApplId = ReadInt16(bytes, 212),
        MaturityYear = ReadUInt16(bytes, 214),
        DecayStartDate = ReadUInt16(bytes, 216),
        ChannelId = ReadUInt16(bytes, 218),
        LegCount = ReadUInt16(bytes, 220),             // ← NEW
        LegIndex = ReadUInt16(bytes, 222),             // ← NEW

        // String fields (offsets 224-486) - ALL FIXED
        Currency = ReadCString(bytes.Slice(224, 4)),                 // ← FIX: was 178
        SettlCurrency = ReadCString(bytes.Slice(228, 4)),            // ← FIX: was 183
        SecSubType = ReadCString(bytes.Slice(232, 6)),               // ← FIX: was 188
        RawSymbol = ReadCString(bytes.Slice(238, 71)),               // ← FIX: was 194, len 22!
        Group = ReadCString(bytes.Slice(309, 21)),                   // ← FIX: was 216
        Exchange = ReadCString(bytes.Slice(330, 5)),                 // ← FIX: was 237
        Asset = ReadCString(bytes.Slice(335, 11)),                   // ← FIX: was 242, len 7!
        Cfi = ReadCString(bytes.Slice(346, 7)),                      // ← FIX: was 249
        SecurityType = ReadCString(bytes.Slice(353, 7)),             // ← FIX: was 256
        UnitOfMeasure = ReadCString(bytes.Slice(360, 31)),           // ← FIX: was 263
        Underlying = ReadCString(bytes.Slice(391, 21)),              // ← FIX: was 294
        StrikePriceCurrency = ReadCString(bytes.Slice(412, 4)),      // ← NEW
        LegRawSymbol = ReadCString(bytes.Slice(416, 71)),            // ← NEW

        // Enum/byte fields (offsets 487-502) - ALL FIXED
        InstrumentClass = (InstrumentClass)bytes[487],               // ← FIX: was 319!
        MatchAlgorithm = (MatchAlgorithm)bytes[488],                 // ← FIX: was 328!
        MainFraction = bytes[489],                                   // ← NEW
        PriceDisplayFormat = bytes[490],                             // ← NEW
        SubFraction = bytes[491],                                    // ← NEW
        UnderlyingProduct = bytes[492],                              // ← NEW
        SecurityUpdateAction = (SecurityUpdateAction)bytes[493],     // ← NEW
        MaturityMonth = bytes[494],                                  // ← NEW
        MaturityDay = bytes[495],                                    // ← NEW
        MaturityWeek = bytes[496],                                   // ← NEW
        UserDefinedInstrument = (UserDefinedInstrument)bytes[497],   // ← NEW
        ContractMultiplierUnit = (sbyte)bytes[498],                  // ← NEW
        FlowScheduleType = (sbyte)bytes[499],                        // ← NEW
        TickRule = bytes[500],                                       // ← NEW
        LegInstrumentClass = (InstrumentClass)bytes[501],            // ← NEW
        LegSide = (Side)bytes[502],                                  // ← NEW
        // Reserved bytes 503-519 ignored
    };
}
```

### Phase 3: Add Safety Net to Enum

**Update `Enums.cs:110-122`** to handle undefined values:

```csharp
public enum InstrumentClass : byte
{
    Unknown = 0,               // ← ADD: Safety net for undefined/reserved
    Bond = (byte)'B',
    Call = (byte)'C',
    Future = (byte)'F',
    Stock = (byte)'K',
    MixedSpread = (byte)'M',
    Put = (byte)'P',
    FutureSpread = (byte)'S',
    OptionSpread = (byte)'T',
    FxSpot = (byte)'X',
    CommoditySpot = (byte)'Y'
}
```

### Phase 4: Comprehensive Testing

**Unit Tests**:
```csharp
[Fact]
public void InstrumentDefMessage_Deserializes_AllFields_Correctly()
{
    // Arrange: Create 520-byte buffer with known values
    byte[] rawBytes = new byte[520];

    // Set header (16 bytes)
    rawBytes[0] = 16; // length
    rawBytes[1] = 0x13; // rtype = InstrumentDef

    // Set instrument_class at correct offset 487
    rawBytes[487] = (byte)'F'; // Future

    // Set raw_symbol at correct offset 238
    var symbolBytes = Encoding.UTF8.GetBytes("ESH25");
    Array.Copy(symbolBytes, 0, rawBytes, 238, symbolBytes.Length);

    // Act
    var record = Record.FromBytes(rawBytes, 0x13);
    var def = record as InstrumentDefMessage;

    // Assert
    Assert.NotNull(def);
    Assert.Equal(InstrumentClass.Future, def.InstrumentClass);
    Assert.Equal("ESH25", def.RawSymbol);
}

[Theory]
[InlineData((byte)'B', InstrumentClass.Bond)]
[InlineData((byte)'C', InstrumentClass.Call)]
[InlineData((byte)'F', InstrumentClass.Future)]
[InlineData((byte)'K', InstrumentClass.Stock)]
[InlineData((byte)'P', InstrumentClass.Put)]
[InlineData(0, InstrumentClass.Unknown)]
public void InstrumentDefMessage_InstrumentClass_MapsCorrectly(byte rawValue, InstrumentClass expected)
{
    byte[] rawBytes = new byte[520];
    rawBytes[1] = 0x13;
    rawBytes[487] = rawValue;

    var record = Record.FromBytes(rawBytes, 0x13);
    var def = record as InstrumentDefMessage;

    Assert.Equal(expected, def.InstrumentClass);
}
```

**Integration Test**:
```csharp
[Fact]
public async Task GLBX_MDP3_Definition_ReturnsCorrectInstrumentClass()
{
    var client = new HistoricalClient(apiKey);

    var records = await client.GetRangeAsync(
        dataset: "GLBX.MDP3",
        schema: Schema.Definition,
        symbols: new[] { "ES.FUT" }, // S&P 500 E-mini future
        startTime: DateTimeOffset.UtcNow.AddDays(-7),
        endTime: DateTimeOffset.UtcNow)
        .Take(10)
        .ToListAsync();

    var defs = records.OfType<InstrumentDefMessage>().ToList();

    Assert.NotEmpty(defs);
    Assert.All(defs, def => {
        Assert.NotEqual(InstrumentClass.Unknown, def.InstrumentClass);
        Assert.Equal(InstrumentClass.Future, def.InstrumentClass); // ES is a future
        Assert.NotEmpty(def.RawSymbol);
    });
}
```

## Breaking Changes

### API Compatibility

**BREAKING CHANGES**:
1. `TradingReferencePrice` should be renamed to `UnitOfMeasureQty` (or kept as deprecated alias)
2. ~20 new properties added to `InstrumentDefMessage`
3. Field values will change for existing properties being read from wrong offsets

**Mitigation**:
- Mark old/incorrect properties as `[Obsolete]` if renamed
- Add XML documentation warnings
- Release as major version (v4.0.0) or clearly document in breaking changes section

### Backward Compatibility Strategy

**Option A: Breaking Release (RECOMMENDED)**
- Release as v4.0.0
- Fix all offsets correctly
- Add all missing fields
- Document breaking changes clearly

**Option B: Gradual Migration**
- Release as v3.1.0-beta with new properties marked as "Experimental"
- Keep old properties but add `[Obsolete]` warnings
- Provide migration guide
- Remove obsolete properties in v4.0.0

## Timeline

### Revised Estimates

- **Investigation**: ✅ Complete (2 hours)
- **Design**: ✅ Complete (1 hour)
- **Implementation**: 8-12 hours (complete rewrite + all new fields)
- **Testing**: 4-6 hours (unit + integration tests)
- **Documentation**: 2-3 hours (XML docs, migration guide, changelog)
- **Review & Release**: 2 hours

**Total**: 17-24 hours (2-3 days of focused work)

## Risk Assessment

### High Risk
- ❌ **Existing code breaks**: Any code reading `InstrumentClass`, `StrikePrice`, or string fields
- ❌ **Data interpretation**: Existing persisted data may have been misinterpreted

### Medium Risk
- ⚠️ **Scope creep**: 20+ new fields to add, test, and document
- ⚠️ **Multi-leg strategies**: New leg-related fields need careful handling

### Low Risk
- ✅ **Fix is well-defined**: DBN spec is clear and authoritative
- ✅ **Easy to validate**: Can compare against C++ implementation byte-for-byte

## Success Criteria

1. ✅ `InstrumentClass` correctly populated from byte offset 487
2. ✅ User can filter futures with `def.InstrumentClass == InstrumentClass.Future`
3. ✅ All string fields read from correct offsets with correct lengths
4. ✅ All 70 fields deserialized correctly
5. ✅ Comprehensive unit tests covering all fields
6. ✅ Integration test with GLBX.MDP3 passes
7. ✅ No regressions in other record types

## References

- [DBN GitHub Repository](https://github.com/databento/dbn)
- [DBN Rust Documentation](https://docs.rs/dbn/latest/dbn/record/struct.InstrumentDefMsg.html)
- [Databento Documentation](https://databento.com/docs/schemas-and-data-formats/instrument-definitions)
- Local C++ source: `build/native/_deps/databento-cpp-src/include/databento/record.hpp`

## Next Steps

1. ⏸️ **User Review**: Approve this comprehensive fix plan
2. ⏸️ **Prioritization**: Decide on v3.1 gradual vs v4.0 breaking release
3. 🔨 **Implementation**: Begin complete rewrite of deserialization
4. 🧪 **Testing**: Write comprehensive test suite
5. 📝 **Documentation**: Update XML docs and migration guide
6. 🚀 **Release**: Publish with clear breaking change warnings
