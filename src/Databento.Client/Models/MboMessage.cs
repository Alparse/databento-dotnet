namespace Databento.Client.Models;

/// <summary>
/// Market-by-Order (MBO) message - 56 bytes
/// Represents individual order book updates at the order level
/// </summary>
public class MboMessage : Record
{
    /// <summary>
    /// Order ID assigned by the venue
    /// </summary>
    public ulong OrderId { get; set; }

    /// <summary>
    /// Order price (fixed-point: value * 10^9)
    /// </summary>
    public long Price { get; set; }

    /// <summary>
    /// Order size/quantity
    /// </summary>
    public uint Size { get; set; }

    /// <summary>
    /// Additional flags
    /// </summary>
    public byte Flags { get; set; }

    /// <summary>
    /// Channel ID within the venue
    /// </summary>
    public byte ChannelId { get; set; }

    /// <summary>
    /// Market action (Add, Modify, Cancel, etc.)
    /// </summary>
    public Action Action { get; set; }

    /// <summary>
    /// Order side (Bid or Ask)
    /// </summary>
    public Side Side { get; set; }

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
        return $"MBO: Order {OrderId} @ {PriceDecimal} x {Size} ({Side}/{Action}) [{Timestamp:O}]";
    }
}
