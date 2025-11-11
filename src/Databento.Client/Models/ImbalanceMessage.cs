namespace Databento.Client.Models;

/// <summary>
/// Imbalance message - 112 bytes
/// Auction imbalance information
/// </summary>
public class ImbalanceMessage : Record
{
    /// <summary>
    /// Timestamp when received by Databento (nanoseconds)
    /// </summary>
    public long TsRecv { get; set; }

    /// <summary>
    /// Reference price for the auction (fixed-point)
    /// </summary>
    public long RefPrice { get; set; }

    /// <summary>
    /// Auction time (nanoseconds)
    /// </summary>
    public long AuctionTime { get; set; }

    /// <summary>
    /// Continuous book clearing price (fixed-point)
    /// </summary>
    public long ContBookClrPrice { get; set; }

    /// <summary>
    /// Auction interest clearing price (fixed-point)
    /// </summary>
    public long AuctionInterestClrPrice { get; set; }

    /// <summary>
    /// SSR (Short Sale Restriction) filling price (fixed-point)
    /// </summary>
    public long SsrFillingPrice { get; set; }

    /// <summary>
    /// Indicative match price (fixed-point)
    /// </summary>
    public long IndMatchPrice { get; set; }

    /// <summary>
    /// Upper collar (fixed-point)
    /// </summary>
    public long UpperCollar { get; set; }

    /// <summary>
    /// Lower collar (fixed-point)
    /// </summary>
    public long LowerCollar { get; set; }

    /// <summary>
    /// Paired quantity
    /// </summary>
    public ulong PairedQty { get; set; }

    /// <summary>
    /// Total imbalance quantity
    /// </summary>
    public ulong TotalImbalanceQty { get; set; }

    /// <summary>
    /// Market imbalance quantity
    /// </summary>
    public ulong MarketImbalanceQty { get; set; }

    /// <summary>
    /// Unpaired quantity
    /// </summary>
    public ulong UnpairedQty { get; set; }

    /// <summary>
    /// Auction type
    /// </summary>
    public byte AuctionType { get; set; }

    /// <summary>
    /// Imbalance side
    /// </summary>
    public Side Side { get; set; }

    /// <summary>
    /// Auction status
    /// </summary>
    public byte AuctionStatus { get; set; }

    /// <summary>
    /// Freeze status
    /// </summary>
    public byte FreezeStatus { get; set; }

    /// <summary>
    /// Number of extensions
    /// </summary>
    public byte NumExtensions { get; set; }

    /// <summary>
    /// Unpaired side
    /// </summary>
    public Side UnpairedSide { get; set; }

    /// <summary>
    /// Significant imbalance
    /// </summary>
    public TriState SignificantImbalance { get; set; }

    public DateTimeOffset TsRecvTime =>
        DateTimeOffset.FromUnixTimeMilliseconds(TsRecv / 1_000_000);

    public decimal RefPriceDecimal => RefPrice / 1_000_000_000m;

    public override string ToString()
    {
        return $"Imbalance: {Side} {TotalImbalanceQty} @ {RefPriceDecimal} [{Timestamp:O}]";
    }
}
