namespace Databento.Client.Models;

/// <summary>
/// Record type identifier
/// </summary>
public enum RType : byte
{
    Mbp0 = 0x00,
    Mbp1 = 0x01,
    Mbp10 = 0x02,
    OhlcvDeprecated = 0x11,
    Ohlcv1S = 0x12,
    Ohlcv1M = 0x13,
    Ohlcv1H = 0x14,
    Ohlcv1D = 0x15,
    OhlcvEod = 0x16,
    Status = 0x17,
    InstrumentDef = 0x18,
    Imbalance = 0x19,
    Error = 0x1A,
    SymbolMapping = 0x1B,
    System = 0x1C,
    Statistics = 0x1D,
    Mbo = 0xA0,
    Cmbp1 = 0xB1,
    Cbbo1S = 0xB2,
    Cbbo1M = 0xB3,
    Tcbbo = 0xB4,
    Bbo1S = 0xC2,
    Bbo1M = 0xC3
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
