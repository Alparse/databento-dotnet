namespace Databento.Client.Models;

/// <summary>
/// Trade message record
/// </summary>
public class TradeMessage : Record
{
    /// <summary>
    /// Trade price
    /// </summary>
    public long Price { get; set; }

    /// <summary>
    /// Trade size (volume)
    /// </summary>
    public uint Size { get; set; }

    /// <summary>
    /// Trade action
    /// </summary>
    public Action Action { get; set; }

    /// <summary>
    /// Trade side (bid/ask)
    /// </summary>
    public Side Side { get; set; }

    /// <summary>
    /// Trade flags
    /// </summary>
    public byte Flags { get; set; }

    /// <summary>
    /// Trade depth
    /// </summary>
    public byte Depth { get; set; }

    /// <summary>
    /// Sequence number
    /// </summary>
    public uint Sequence { get; set; }

    /// <summary>
    /// Get price as decimal (price is in fixed-point format)
    /// </summary>
    public decimal PriceDecimal => Price / 1_000_000_000m;

    public override string ToString()
    {
        return $"Trade: {InstrumentId} @ {PriceDecimal} x {Size} ({Side}) [{Timestamp:O}]";
    }
}
