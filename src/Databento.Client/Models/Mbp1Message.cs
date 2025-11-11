namespace Databento.Client.Models;

/// <summary>
/// Market-by-Price depth-1 (MBP-1) message - 80 bytes
/// Top of book aggregated by price level
/// </summary>
public class Mbp1Message : Record
{
    /// <summary>
    /// Top-level price (fixed-point: value * 10^9)
    /// </summary>
    public long Price { get; set; }

    /// <summary>
    /// Top-level size/quantity
    /// </summary>
    public uint Size { get; set; }

    /// <summary>
    /// Market action
    /// </summary>
    public Action Action { get; set; }

    /// <summary>
    /// Order side
    /// </summary>
    public Side Side { get; set; }

    /// <summary>
    /// Additional flags
    /// </summary>
    public byte Flags { get; set; }

    /// <summary>
    /// Price depth level (typically 0 for top of book)
    /// </summary>
    public byte Depth { get; set; }

    /// <summary>
    /// Timestamp when received by Databento (nanoseconds)
    /// </summary>
    public long TsRecv { get; set; }

    /// <summary>
    /// Time delta from exchange timestamp (nanoseconds)
    /// </summary>
    public int TsInDelta { get; set; }

    /// <summary>
    /// Message sequence number
    /// </summary>
    public uint Sequence { get; set; }

    /// <summary>
    /// Bid/Ask pair for this level
    /// </summary>
    public BidAskPair Level { get; set; }

    /// <summary>
    /// Helper property for decimal price
    /// </summary>
    public decimal PriceDecimal => Price / 1_000_000_000m;

    /// <summary>
    /// Get receive timestamp as DateTimeOffset
    /// </summary>
    public DateTimeOffset TsRecvTime =>
        DateTimeOffset.FromUnixTimeMilliseconds(TsRecv / 1_000_000);

    public override string ToString()
    {
        return $"MBP1: {PriceDecimal} x {Size} ({Side}/{Action}) | Level: {Level} [{Timestamp:O}]";
    }
}
