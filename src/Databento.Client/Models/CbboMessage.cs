namespace Databento.Client.Models;

/// <summary>
/// Consolidated Best Bid/Offer (CBBO) message - 80 bytes
/// Consolidated top of book across venues
/// </summary>
public class CbboMessage : Record
{
    /// <summary>
    /// Price (fixed-point: value * 10^9)
    /// </summary>
    public long Price { get; set; }

    /// <summary>
    /// Size/quantity
    /// </summary>
    public uint Size { get; set; }

    /// <summary>
    /// Order side
    /// </summary>
    public Side Side { get; set; }

    /// <summary>
    /// Additional flags
    /// </summary>
    public byte Flags { get; set; }

    /// <summary>
    /// Timestamp when received by Databento (nanoseconds)
    /// </summary>
    public long TsRecv { get; set; }

    /// <summary>
    /// Message sequence number
    /// </summary>
    public uint Sequence { get; set; }

    /// <summary>
    /// Consolidated Bid/Ask pair
    /// </summary>
    public ConsolidatedBidAskPair Level { get; set; }

    public decimal PriceDecimal => Price / 1_000_000_000m;

    public DateTimeOffset TsRecvTime =>
        DateTimeOffset.FromUnixTimeMilliseconds(TsRecv / 1_000_000);

    public override string ToString()
    {
        return $"CBBO: {PriceDecimal} x {Size} ({Side}) | Level: {Level} [{Timestamp:O}]";
    }
}
