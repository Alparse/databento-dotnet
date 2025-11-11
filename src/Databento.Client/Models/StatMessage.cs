namespace Databento.Client.Models;

/// <summary>
/// Statistics message - 80 bytes
/// Market statistics and derived data
/// </summary>
public class StatMessage : Record
{
    /// <summary>
    /// Timestamp when received by Databento (nanoseconds)
    /// </summary>
    public long TsRecv { get; set; }

    /// <summary>
    /// Reference timestamp (nanoseconds)
    /// </summary>
    public long TsRef { get; set; }

    /// <summary>
    /// Statistics price value (fixed-point)
    /// </summary>
    public long Price { get; set; }

    /// <summary>
    /// Statistics quantity value
    /// </summary>
    public long Quantity { get; set; }

    /// <summary>
    /// Message sequence number
    /// </summary>
    public uint Sequence { get; set; }

    /// <summary>
    /// Time delta from exchange timestamp (nanoseconds)
    /// </summary>
    public int TsInDelta { get; set; }

    /// <summary>
    /// Type of statistic
    /// </summary>
    public ushort StatType { get; set; }

    /// <summary>
    /// Channel ID
    /// </summary>
    public ushort ChannelId { get; set; }

    /// <summary>
    /// Update action type
    /// </summary>
    public byte UpdateAction { get; set; }

    /// <summary>
    /// Additional flags for this statistic
    /// </summary>
    public byte StatFlags { get; set; }

    public DateTimeOffset TsRecvTime =>
        DateTimeOffset.FromUnixTimeMilliseconds(TsRecv / 1_000_000);

    public DateTimeOffset TsRefTime =>
        DateTimeOffset.FromUnixTimeMilliseconds(TsRef / 1_000_000);

    public decimal PriceDecimal => Price / 1_000_000_000m;

    public override string ToString()
    {
        return $"Stat: Type={StatType} Price={PriceDecimal} Qty={Quantity} [{Timestamp:O}]";
    }
}
