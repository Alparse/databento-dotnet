namespace Databento.Client.Models;

/// <summary>
/// Instrument definition message - 520 bytes
/// Contains detailed static information about an instrument
/// </summary>
public class InstrumentDefMessage : Record
{
    /// <summary>
    /// Timestamp when received by Databento (nanoseconds)
    /// </summary>
    public long TsRecv { get; set; }

    /// <summary>
    /// Minimum price increment (tick size, fixed-point)
    /// </summary>
    public long MinPriceIncrement { get; set; }

    /// <summary>
    /// Display factor for price formatting
    /// </summary>
    public long DisplayFactor { get; set; }

    /// <summary>
    /// Expiration timestamp (nanoseconds)
    /// </summary>
    public long Expiration { get; set; }

    /// <summary>
    /// Activation timestamp (nanoseconds)
    /// </summary>
    public long Activation { get; set; }

    /// <summary>
    /// High limit price (fixed-point)
    /// </summary>
    public long HighLimitPrice { get; set; }

    /// <summary>
    /// Low limit price (fixed-point)
    /// </summary>
    public long LowLimitPrice { get; set; }

    /// <summary>
    /// Maximum price variation (fixed-point)
    /// </summary>
    public long MaxPriceVariation { get; set; }

    /// <summary>
    /// Unit of measure quantity
    /// </summary>
    public long UnitOfMeasureQty { get; set; }

    /// <summary>
    /// Minimum price increment amount
    /// </summary>
    public long MinPriceIncrementAmount { get; set; }

    /// <summary>
    /// Price ratio (fixed-point)
    /// </summary>
    public long PriceRatio { get; set; }

    /// <summary>
    /// Strike price (fixed-point)
    /// </summary>
    public long StrikePrice { get; set; }

    /// <summary>
    /// Leg price for multi-leg strategies (fixed-point)
    /// </summary>
    public long LegPrice { get; set; }

    /// <summary>
    /// Leg delta for multi-leg strategies (fixed-point)
    /// </summary>
    public long LegDelta { get; set; }

    /// <summary>
    /// Instrument attribute value
    /// </summary>
    public int InstAttribValue { get; set; }

    /// <summary>
    /// Underlying instrument ID
    /// </summary>
    public uint UnderlyingId { get; set; }

    /// <summary>
    /// Raw instrument ID.
    /// Note: DBN specification defines this as 64-bit, but this implementation uses 32-bit uint
    /// for backward compatibility. An exception will be thrown if a venue provides an ID exceeding
    /// uint.MaxValue (4,294,967,295).
    /// </summary>
    public uint RawInstrumentId { get; set; }

    /// <summary>
    /// Market depth (implied)
    /// </summary>
    public int MarketDepthImplied { get; set; }

    /// <summary>
    /// Market depth
    /// </summary>
    public int MarketDepth { get; set; }

    /// <summary>
    /// Market segment ID
    /// </summary>
    public uint MarketSegmentId { get; set; }

    /// <summary>
    /// Maximum trade volume
    /// </summary>
    public uint MaxTradeVol { get; set; }

    /// <summary>
    /// Minimum lot size
    /// </summary>
    public int MinLotSize { get; set; }

    /// <summary>
    /// Minimum lot size for block trades
    /// </summary>
    public int MinLotSizeBlock { get; set; }

    /// <summary>
    /// Minimum lot size for round lots
    /// </summary>
    public int MinLotSizeRoundLot { get; set; }

    /// <summary>
    /// Minimum trade volume
    /// </summary>
    public uint MinTradeVol { get; set; }

    /// <summary>
    /// Contract multiplier
    /// </summary>
    public int ContractMultiplier { get; set; }

    /// <summary>
    /// Decay quantity
    /// </summary>
    public int DecayQuantity { get; set; }

    /// <summary>
    /// Original contract size
    /// </summary>
    public int OriginalContractSize { get; set; }

    /// <summary>
    /// Leg instrument ID for multi-leg strategies
    /// </summary>
    public uint LegInstrumentId { get; set; }

    /// <summary>
    /// Leg price ratio numerator for multi-leg strategies
    /// </summary>
    public int LegRatioPriceNumerator { get; set; }

    /// <summary>
    /// Leg price ratio denominator for multi-leg strategies
    /// </summary>
    public int LegRatioPriceDenominator { get; set; }

    /// <summary>
    /// Leg quantity ratio numerator for multi-leg strategies
    /// </summary>
    public int LegRatioQtyNumerator { get; set; }

    /// <summary>
    /// Leg quantity ratio denominator for multi-leg strategies
    /// </summary>
    public int LegRatioQtyDenominator { get; set; }

    /// <summary>
    /// Leg underlying instrument ID for multi-leg strategies
    /// </summary>
    public uint LegUnderlyingId { get; set; }

    /// <summary>
    /// Application ID
    /// </summary>
    public short ApplId { get; set; }

    /// <summary>
    /// Maturity year
    /// </summary>
    public ushort MaturityYear { get; set; }

    /// <summary>
    /// Decay start date
    /// </summary>
    public ushort DecayStartDate { get; set; }

    /// <summary>
    /// Channel ID
    /// </summary>
    public ushort ChannelId { get; set; }

    /// <summary>
    /// Number of legs in multi-leg strategies (spreads, combos)
    /// </summary>
    public ushort LegCount { get; set; }

    /// <summary>
    /// Leg index for multi-leg strategies (0-based)
    /// </summary>
    public ushort LegIndex { get; set; }

    /// <summary>
    /// Currency code (e.g., "USD")
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Settlement currency code
    /// </summary>
    public string SettlCurrency { get; set; } = string.Empty;

    /// <summary>
    /// Security subtype
    /// </summary>
    public string SecSubType { get; set; } = string.Empty;

    /// <summary>
    /// Raw symbol string
    /// </summary>
    public string RawSymbol { get; set; } = string.Empty;

    /// <summary>
    /// Security group
    /// </summary>
    public string Group { get; set; } = string.Empty;

    /// <summary>
    /// Exchange code
    /// </summary>
    public string Exchange { get; set; } = string.Empty;

    /// <summary>
    /// Asset class
    /// </summary>
    public string Asset { get; set; } = string.Empty;

    /// <summary>
    /// CFI code (Classification of Financial Instruments)
    /// </summary>
    public string Cfi { get; set; } = string.Empty;

    /// <summary>
    /// Security type
    /// </summary>
    public string SecurityType { get; set; } = string.Empty;

    /// <summary>
    /// Unit of measure
    /// </summary>
    public string UnitOfMeasure { get; set; } = string.Empty;

    /// <summary>
    /// Underlying symbol
    /// </summary>
    public string Underlying { get; set; } = string.Empty;

    /// <summary>
    /// Strike price currency
    /// </summary>
    public string StrikePriceCurrency { get; set; } = string.Empty;

    /// <summary>
    /// Raw symbol for leg instrument in multi-leg strategies
    /// </summary>
    public string LegRawSymbol { get; set; } = string.Empty;

    /// <summary>
    /// Instrument class
    /// </summary>
    public InstrumentClass InstrumentClass { get; set; }

    /// <summary>
    /// Match algorithm
    /// </summary>
    public MatchAlgorithm MatchAlgorithm { get; set; }

    /// <summary>
    /// Market data security trading status
    /// </summary>
    public byte MdSecurityTradingStatus { get; set; }

    /// <summary>
    /// Main fraction
    /// </summary>
    public byte MainFraction { get; set; }

    /// <summary>
    /// Price display format
    /// </summary>
    public byte PriceDisplayFormat { get; set; }

    /// <summary>
    /// Settlement price type
    /// </summary>
    public byte SettlPriceType { get; set; }

    /// <summary>
    /// Sub-fraction
    /// </summary>
    public byte SubFraction { get; set; }

    /// <summary>
    /// Underlying product
    /// </summary>
    public byte UnderlyingProduct { get; set; }

    /// <summary>
    /// Security update action
    /// </summary>
    public SecurityUpdateAction SecurityUpdateAction { get; set; }

    /// <summary>
    /// Maturity month
    /// </summary>
    public byte MaturityMonth { get; set; }

    /// <summary>
    /// Maturity day
    /// </summary>
    public byte MaturityDay { get; set; }

    /// <summary>
    /// Maturity week
    /// </summary>
    public byte MaturityWeek { get; set; }

    /// <summary>
    /// User-defined instrument flag
    /// </summary>
    public UserDefinedInstrument UserDefinedInstrument { get; set; }

    /// <summary>
    /// Contract multiplier unit
    /// </summary>
    public sbyte ContractMultiplierUnit { get; set; }

    /// <summary>
    /// Flow schedule type
    /// </summary>
    public sbyte FlowScheduleType { get; set; }

    /// <summary>
    /// Tick rule
    /// </summary>
    public byte TickRule { get; set; }

    /// <summary>
    /// Instrument class for leg instrument in multi-leg strategies
    /// </summary>
    public InstrumentClass LegInstrumentClass { get; set; }

    /// <summary>
    /// Side for leg instrument in multi-leg strategies (Ask/Bid/None)
    /// </summary>
    public Side LegSide { get; set; }

    /// <summary>
    /// Get receive timestamp as DateTimeOffset
    /// </summary>
    public DateTimeOffset TsRecvTime =>
        DateTimeOffset.FromUnixTimeMilliseconds(TsRecv / 1_000_000);

    /// <summary>
    /// Get expiration as DateTimeOffset
    /// </summary>
    public DateTimeOffset ExpirationTime =>
        DateTimeOffset.FromUnixTimeMilliseconds(Expiration / 1_000_000);

    /// <summary>
    /// Get strike price as decimal (fixed-point format with 9 decimal places).
    /// Returns null if the value is UNDEF_PRICE (long.MaxValue).
    /// Note: Can be negative for certain instrument types (e.g., calendar spreads).
    /// </summary>
    public decimal? StrikePriceDecimal => StrikePrice == long.MaxValue ? null : StrikePrice / 1_000_000_000m;

    /// <summary>
    /// Get high limit price as decimal (fixed-point format with 9 decimal places).
    /// Returns null if the value is UNDEF_PRICE (long.MaxValue).
    /// Note: Can be negative for certain instrument types.
    /// </summary>
    public decimal? HighLimitPriceDecimal => HighLimitPrice == long.MaxValue ? null : HighLimitPrice / 1_000_000_000m;

    /// <summary>
    /// Get low limit price as decimal (fixed-point format with 9 decimal places).
    /// Returns null if the value is UNDEF_PRICE (long.MaxValue).
    /// Note: Can be negative for certain instrument types.
    /// </summary>
    public decimal? LowLimitPriceDecimal => LowLimitPrice == long.MaxValue ? null : LowLimitPrice / 1_000_000_000m;

    /// <summary>
    /// Get maximum price variation as decimal (fixed-point format with 9 decimal places).
    /// Returns null if the value is UNDEF_PRICE (long.MaxValue).
    /// Note: Can be negative for certain instrument types.
    /// </summary>
    public decimal? MaxPriceVariationDecimal => MaxPriceVariation == long.MaxValue ? null : MaxPriceVariation / 1_000_000_000m;

    /// <summary>
    /// Get minimum price increment as decimal (fixed-point format with 9 decimal places).
    /// Returns null if the value is UNDEF_PRICE (long.MaxValue).
    /// Note: Can be negative for certain instrument types.
    /// </summary>
    public decimal? MinPriceIncrementDecimal => MinPriceIncrement == long.MaxValue ? null : MinPriceIncrement / 1_000_000_000m;

    /// <summary>
    /// Get minimum price increment amount as decimal (fixed-point format with 9 decimal places).
    /// Returns null if the value is UNDEF_PRICE (long.MaxValue).
    /// Note: Can be negative for certain instrument types.
    /// </summary>
    public decimal? MinPriceIncrementAmountDecimal => MinPriceIncrementAmount == long.MaxValue ? null : MinPriceIncrementAmount / 1_000_000_000m;

    /// <summary>
    /// Get price ratio as decimal (fixed-point format with 9 decimal places).
    /// Returns null if the value is UNDEF_PRICE (long.MaxValue).
    /// Note: Can be negative for certain instrument types.
    /// </summary>
    public decimal? PriceRatioDecimal => PriceRatio == long.MaxValue ? null : PriceRatio / 1_000_000_000m;

    /// <summary>
    /// Get leg price as decimal for multi-leg strategies (fixed-point format with 9 decimal places).
    /// Returns null if the value is UNDEF_PRICE (long.MaxValue).
    /// Note: Can be negative for certain instrument types.
    /// </summary>
    public decimal? LegPriceDecimal => LegPrice == long.MaxValue ? null : LegPrice / 1_000_000_000m;

    /// <summary>
    /// Get leg delta as decimal for multi-leg strategies (fixed-point format with 9 decimal places).
    /// Returns null if the value is UNDEF_PRICE (long.MaxValue).
    /// Note: Can be negative (deltas are often negative for puts).
    /// </summary>
    public decimal? LegDeltaDecimal => LegDelta == long.MaxValue ? null : LegDelta / 1_000_000_000m;

    public override string ToString()
    {
        return $"InstrumentDef: {RawSymbol} ({Exchange}) | {SecurityType} {InstrumentClass} [{Timestamp:O}]";
    }
}
