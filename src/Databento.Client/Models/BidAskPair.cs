namespace Databento.Client.Models;

/// <summary>
/// Bid/Ask price level pair - 32 bytes
/// </summary>
public struct BidAskPair
{
    /// <summary>
    /// Bid price (fixed-point: value * 10^9)
    /// </summary>
    public long BidPrice { get; set; }

    /// <summary>
    /// Ask price (fixed-point: value * 10^9)
    /// </summary>
    public long AskPrice { get; set; }

    /// <summary>
    /// Bid size/quantity
    /// </summary>
    public uint BidSize { get; set; }

    /// <summary>
    /// Ask size/quantity
    /// </summary>
    public uint AskSize { get; set; }

    /// <summary>
    /// Bid order count
    /// </summary>
    public uint BidCount { get; set; }

    /// <summary>
    /// Ask order count
    /// </summary>
    public uint AskCount { get; set; }

    /// <summary>
    /// Bid price as decimal
    /// </summary>
    public decimal BidPriceDecimal => BidPrice / 1_000_000_000m;

    /// <summary>
    /// Ask price as decimal
    /// </summary>
    public decimal AskPriceDecimal => AskPrice / 1_000_000_000m;

    public override string ToString()
    {
        return $"{BidPriceDecimal}x{BidSize}({BidCount}) | {AskPriceDecimal}x{AskSize}({AskCount})";
    }
}
