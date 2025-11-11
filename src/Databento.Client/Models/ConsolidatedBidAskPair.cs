namespace Databento.Client.Models;

/// <summary>
/// Consolidated Bid/Ask price level pair - 32 bytes
/// Includes publisher information
/// </summary>
public struct ConsolidatedBidAskPair
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
    /// Bid publisher ID
    /// </summary>
    public ushort BidPublisher { get; set; }

    /// <summary>
    /// Ask publisher ID
    /// </summary>
    public ushort AskPublisher { get; set; }

    public decimal BidPriceDecimal => BidPrice / 1_000_000_000m;
    public decimal AskPriceDecimal => AskPrice / 1_000_000_000m;

    public override string ToString()
    {
        return $"{BidPriceDecimal}x{BidSize}(Pub:{BidPublisher}) | {AskPriceDecimal}x{AskSize}(Pub:{AskPublisher})";
    }
}
