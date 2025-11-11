namespace Databento.Client.Models;

/// <summary>
/// Best Bid/Offer (BBO) message - 80 bytes
/// Top of book for a single venue
/// </summary>
public class BboMessage : Record
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
    /// Bid/Ask pair
    /// </summary>
    public BidAskPair Level { get; set; }

    public decimal PriceDecimal => Price / 1_000_000_000m;

    public DateTimeOffset TsRecvTime =>
        DateTimeOffset.FromUnixTimeMilliseconds(TsRecv / 1_000_000);

    public override string ToString()
    {
        return $"BBO: {PriceDecimal} x {Size} ({Side}) | Level: {Level} [{Timestamp:O}]";
    }
}
