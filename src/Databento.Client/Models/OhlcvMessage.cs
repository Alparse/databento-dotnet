namespace Databento.Client.Models;

/// <summary>
/// OHLCV (Open, High, Low, Close, Volume) bar message - 56 bytes
/// Aggregated price bars for various time periods (1s, 1m, 1h, 1d, EOD)
/// </summary>
public class OhlcvMessage : Record
{
    /// <summary>
    /// Opening price (fixed-point: value * 10^9)
    /// </summary>
    public long Open { get; set; }

    /// <summary>
    /// Highest price (fixed-point: value * 10^9)
    /// </summary>
    public long High { get; set; }

    /// <summary>
    /// Lowest price (fixed-point: value * 10^9)
    /// </summary>
    public long Low { get; set; }

    /// <summary>
    /// Closing price (fixed-point: value * 10^9)
    /// </summary>
    public long Close { get; set; }

    /// <summary>
    /// Trading volume
    /// </summary>
    public ulong Volume { get; set; }

    /// <summary>
    /// Opening price as decimal
    /// </summary>
    public decimal OpenDecimal => Open / 1_000_000_000m;

    /// <summary>
    /// Highest price as decimal
    /// </summary>
    public decimal HighDecimal => High / 1_000_000_000m;

    /// <summary>
    /// Lowest price as decimal
    /// </summary>
    public decimal LowDecimal => Low / 1_000_000_000m;

    /// <summary>
    /// Closing price as decimal
    /// </summary>
    public decimal CloseDecimal => Close / 1_000_000_000m;

    public override string ToString()
    {
        return $"OHLCV: O:{OpenDecimal} H:{HighDecimal} L:{LowDecimal} C:{CloseDecimal} V:{Volume} [{Timestamp:O}]";
    }
}
