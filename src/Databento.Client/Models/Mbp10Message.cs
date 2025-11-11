namespace Databento.Client.Models;

/// <summary>
/// Market-by-Price depth-10 (MBP-10) message - 368 bytes
/// Top 10 levels of the order book aggregated by price
/// </summary>
public class Mbp10Message : Record
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
    /// Price depth level
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
    /// Top 10 bid/ask price levels
    /// </summary>
    public BidAskPair[] Levels { get; set; } = new BidAskPair[10];

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
        return $"MBP10: {PriceDecimal} x {Size} ({Side}/{Action}) | Top Level: {Levels[0]} [{Timestamp:O}]";
    }
}
