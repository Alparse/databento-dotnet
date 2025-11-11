namespace Databento.Client.Models;

/// <summary>
/// Consolidated Market-by-Price depth-1 (CMBP-1) message - 80 bytes
/// Consolidated top of book aggregated across venues
/// </summary>
public class Cmbp1Message : Record
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
    /// Timestamp when received by Databento (nanoseconds)
    /// </summary>
    public long TsRecv { get; set; }

    /// <summary>
    /// Time delta from exchange timestamp (nanoseconds)
    /// </summary>
    public int TsInDelta { get; set; }

    /// <summary>
    /// Consolidated Bid/Ask pair for this level
    /// </summary>
    public ConsolidatedBidAskPair Level { get; set; }

    public decimal PriceDecimal => Price / 1_000_000_000m;

    public DateTimeOffset TsRecvTime =>
        DateTimeOffset.FromUnixTimeMilliseconds(TsRecv / 1_000_000);

    public override string ToString()
    {
        return $"CMBP1: {PriceDecimal} x {Size} ({Side}/{Action}) | Level: {Level} [{Timestamp:O}]";
    }
}
