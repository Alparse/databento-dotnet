namespace Databento.Client.Models;

/// <summary>
/// Record type identifier - Values match databento-cpp enums.hpp
/// </summary>
public enum RType : byte
{
    Mbp0 = 0x00,              // Trade messages
    Mbp1 = 0x01,              // Market by Price Level 1
    Mbp10 = 0x0A,             // FIXED: was 0x02, correct is 0x0A
    OhlcvDeprecated = 0x11,   // Deprecated OHLCV
    Status = 0x12,            // FIXED: was 0x17, correct is 0x12 - Trading status
    InstrumentDef = 0x13,     // FIXED: was 0x18, correct is 0x13 - Instrument definitions
    Imbalance = 0x14,         // FIXED: was 0x19, correct is 0x14 - Order imbalances
    Error = 0x15,             // FIXED: was 0x1A, correct is 0x15 - Error messages
    SymbolMapping = 0x16,     // FIXED: was 0x1B, correct is 0x16 - Symbol mappings
    System = 0x17,            // FIXED: was 0x1C, correct is 0x17 - System messages / heartbeats
    Statistics = 0x18,        // FIXED: was 0x1D, correct is 0x18 - Market statistics
    Ohlcv1S = 0x20,           // FIXED: was 0x12, correct is 0x20 - OHLCV 1 second
    Ohlcv1M = 0x21,           // FIXED: was 0x13, correct is 0x21 - OHLCV 1 minute
    Ohlcv1H = 0x22,           // FIXED: was 0x14, correct is 0x22 - OHLCV 1 hour
    Ohlcv1D = 0x23,           // FIXED: was 0x15, correct is 0x23 - OHLCV 1 day
    OhlcvEod = 0x24,          // FIXED: was 0x16, correct is 0x24 - OHLCV end of day
    Mbo = 0xA0,               // Market by Order
    Cmbp1 = 0xB1,             // Consolidated Market by Price Level 1
    Cbbo1S = 0xC0,            // Consolidated Best Bid/Offer 1 second
    Cbbo1M = 0xC1,            // Consolidated Best Bid/Offer 1 minute
    Tcbbo = 0xC2,             // Trade with Consolidated BBO
    Bbo1S = 0xC3,             // Best Bid/Offer 1 second
    Bbo1M = 0xC4              // Best Bid/Offer 1 minute
}

/// <summary>
/// Order side
/// </summary>
public enum Side : byte
{
    Ask = (byte)'A',
    Bid = (byte)'B',
    None = (byte)'N'
}

/// <summary>
/// Market action type
/// </summary>
public enum Action : byte
{
    Modify = (byte)'M',
    Trade = (byte)'T',
    Fill = (byte)'F',
    Cancel = (byte)'C',
    Add = (byte)'A',
    Clear = (byte)'R',
    None = (byte)'N'
}

/// <summary>
/// Symbol type (symbology)
/// </summary>
public enum SType : byte
{
    InstrumentId = 0,
    RawSymbol = 1,
    Smart = 2,
    Continuous = 3,
    Parent = 4,
    NasdaqSymbol = 5,
    CmsSymbol = 6,
    Isin = 7,
    UsCode = 8,
    BbgCompId = 9,
    BbgCompTicker = 10,
    Figi = 11,
    FigiTicker = 12
}

/// <summary>
/// Extension methods for SType enum
/// </summary>
public static class STypeExtensions
{
    /// <summary>
    /// Convert SType enum to string representation for API calls
    /// </summary>
    public static string ToStypeString(this SType stype)
    {
        return stype switch
        {
            SType.InstrumentId => "instrument_id",
            SType.RawSymbol => "raw_symbol",
            SType.Smart => "smart",
            SType.Continuous => "continuous",
            SType.Parent => "parent",
            SType.NasdaqSymbol => "nasdaq_symbol",
            SType.CmsSymbol => "cms_symbol",
            SType.Isin => "isin",
            SType.UsCode => "us_code",
            SType.BbgCompId => "bbg_comp_id",
            SType.BbgCompTicker => "bbg_comp_ticker",
            SType.Figi => "figi",
            SType.FigiTicker => "figi_ticker",
            _ => throw new ArgumentOutOfRangeException(nameof(stype))
        };
    }
}

/// <summary>
/// Instrument class type
/// </summary>
public enum InstrumentClass : byte
{
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

/// <summary>
/// Match algorithm type
/// </summary>
public enum MatchAlgorithm : byte
{
    Undefined = (byte)'0',
    Fifo = (byte)'F',
    Configurable = (byte)'K',
    ProRata = (byte)'C',
    FifoLmm = (byte)'T',
    ThresholdProRata = (byte)'O',
    FifoTopLmm = (byte)'S',
    ThresholdProRataLmm = (byte)'Q',
    Eurodollar = (byte)'Y'
}

/// <summary>
/// User-defined instrument indicator
/// </summary>
public enum UserDefinedInstrument : byte
{
    No = (byte)'N',
    Yes = (byte)'Y'
}

/// <summary>
/// Security update action
/// </summary>
public enum SecurityUpdateAction : byte
{
    Add = (byte)'A',
    Modify = (byte)'M',
    Delete = (byte)'D'
}

/// <summary>
/// Trading status action
/// </summary>
public enum StatusAction : byte
{
    None = 0,
    PreOpen = 1,
    PreCross = 2,
    Quoting = 3,
    Cross = 4,
    Rotation = 5,
    NewPriceIndication = 6,
    Trading = 7,
    Halt = 8,
    Pause = 9,
    Suspend = 10,
    PreClose = 11,
    Close = 12,
    PostClose = 13,
    Closed = 14,
    PrivateAuction = 200
}

/// <summary>
/// Trading status reason
/// </summary>
public enum StatusReason : ushort
{
    None = 0,
    Scheduled = 1,
    SurveillanceIntervention = 2,
    MarketEvent = 3,
    InstrumentActivation = 4,
    InstrumentExpiration = 5,
    Recovery = 6,
    Compliance = 7,
    Regulatory = 8,
    AdministrativeEnd = 9,
    AdministrativeSuspend = 10,
    NotAvailable = 11
}

/// <summary>
/// Trading event type
/// </summary>
public enum TradingEvent : byte
{
    None = 0,
    NoCancel = 1,
    ChangeTradingSession = 2,
    ImpliedMatchingOn = 3,
    ImpliedMatchingOff = 4
}

/// <summary>
/// Tri-state value
/// </summary>
public enum TriState : byte
{
    NotAvailable = (byte)'~',
    No = (byte)'N',
    Yes = (byte)'Y'
}

/// <summary>
/// Error code indicating the type of error message (matches databento-cpp ErrorCode)
/// </summary>
public enum ErrorCode : byte
{
    /// <summary>
    /// The authentication step failed
    /// </summary>
    AuthFailed = 1,

    /// <summary>
    /// The user account or API key were deactivated
    /// </summary>
    ApiKeyDeactivated = 2,

    /// <summary>
    /// The user has exceeded their open connection limit
    /// </summary>
    ConnectionLimitExceeded = 3,

    /// <summary>
    /// One or more symbols failed to resolve
    /// </summary>
    SymbolResolutionFailed = 4,

    /// <summary>
    /// There was an issue with a subscription request (other than symbol resolution)
    /// </summary>
    InvalidSubscription = 5,

    /// <summary>
    /// An error occurred in the gateway
    /// </summary>
    InternalError = 6,

    /// <summary>
    /// No error code was specified or this record was upgraded from a version 1 struct where the code field didn't exist
    /// </summary>
    Unset = 255
}

/// <summary>
/// System message code indicating the type of system message (matches databento-cpp SystemCode)
/// </summary>
public enum SystemCode : byte
{
    /// <summary>
    /// A message sent in the absence of other records to indicate the connection remains open
    /// </summary>
    Heartbeat = 0,

    /// <summary>
    /// An acknowledgement of a subscription request
    /// </summary>
    SubscriptionAck = 1,

    /// <summary>
    /// The gateway has detected this session is falling behind real-time
    /// </summary>
    SlowReaderWarning = 2,

    /// <summary>
    /// Indicates a replay subscription has caught up with real-time data
    /// </summary>
    ReplayCompleted = 3,

    /// <summary>
    /// Signals that all records for interval-based schemas have been published for the given timestamp
    /// </summary>
    EndOfInterval = 4,

    /// <summary>
    /// No system code was specified or this record was upgraded from a version 1 struct where the code field didn't exist
    /// </summary>
    Unset = 255
}
