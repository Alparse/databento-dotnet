namespace Databento.Client.Models;

/// <summary>
/// Databento schema types
/// </summary>
public enum Schema : ushort
{
    /// <summary>Market by order</summary>
    Mbo = 0,

    /// <summary>Market by price level 1</summary>
    Mbp1 = 1,

    /// <summary>Market by price level 10</summary>
    Mbp10 = 2,

    /// <summary>All trade events with BBO immediately before the trade</summary>
    Tbbo = 3,

    /// <summary>All trades</summary>
    Trades = 4,

    /// <summary>OHLCV 1 second bars</summary>
    Ohlcv1S = 5,

    /// <summary>OHLCV 1 minute bars</summary>
    Ohlcv1M = 6,

    /// <summary>OHLCV 1 hour bars</summary>
    Ohlcv1H = 7,

    /// <summary>OHLCV 1 day bars (UTC)</summary>
    Ohlcv1D = 8,

    /// <summary>Instrument definitions</summary>
    Definition = 9,

    /// <summary>Additional data disseminated by publishers</summary>
    Statistics = 10,

    /// <summary>Trading status events</summary>
    Status = 11,

    /// <summary>Auction imbalance events</summary>
    Imbalance = 12,

    /// <summary>OHLCV end of day bars (based on trading session)</summary>
    OhlcvEod = 13,

    /// <summary>Consolidated best bid and offer (MBP-1)</summary>
    Cmbp1 = 14,

    /// <summary>Consolidated BBO subsampled at 1-second intervals, with trades</summary>
    Cbbo1S = 15,

    /// <summary>Consolidated BBO subsampled at 1-minute intervals, with trades</summary>
    Cbbo1M = 16,

    /// <summary>All trade events with consolidated BBO immediately before the trade</summary>
    Tcbbo = 17,

    /// <summary>Best bid and offer subsampled at 1-second intervals, with trades</summary>
    Bbo1S = 18,

    /// <summary>Best bid and offer subsampled at 1-minute intervals, with trades</summary>
    Bbo1M = 19
}

/// <summary>
/// Extension methods for Schema enum
/// </summary>
public static class SchemaExtensions
{
    /// <summary>
    /// Convert schema enum to string representation
    /// </summary>
    public static string ToSchemaString(this Schema schema)
    {
        return schema switch
        {
            Schema.Mbo => "mbo",
            Schema.Mbp1 => "mbp-1",
            Schema.Mbp10 => "mbp-10",
            Schema.Tbbo => "tbbo",
            Schema.Trades => "trades",
            Schema.Ohlcv1S => "ohlcv-1s",
            Schema.Ohlcv1M => "ohlcv-1m",
            Schema.Ohlcv1H => "ohlcv-1h",
            Schema.Ohlcv1D => "ohlcv-1d",
            Schema.Definition => "definition",
            Schema.Statistics => "statistics",
            Schema.Status => "status",
            Schema.Imbalance => "imbalance",
            Schema.OhlcvEod => "ohlcv-eod",
            Schema.Cmbp1 => "cmbp-1",
            Schema.Cbbo1S => "cbbo-1s",
            Schema.Cbbo1M => "cbbo-1m",
            Schema.Tcbbo => "tcbbo",
            Schema.Bbo1S => "bbo-1s",
            Schema.Bbo1M => "bbo-1m",
            _ => throw new ArgumentOutOfRangeException(nameof(schema))
        };
    }

    /// <summary>
    /// Parse schema from string
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when schemaString is null</exception>
    /// <exception cref="ArgumentException">Thrown when schemaString is not a valid schema</exception>
    public static Schema ParseSchema(string schemaString)
    {
        ArgumentNullException.ThrowIfNull(schemaString);

        return schemaString.ToLowerInvariant() switch
        {
            "mbo" => Schema.Mbo,
            "mbp-1" => Schema.Mbp1,
            "mbp-10" => Schema.Mbp10,
            "tbbo" => Schema.Tbbo,
            "trades" => Schema.Trades,
            "ohlcv-1s" => Schema.Ohlcv1S,
            "ohlcv-1m" => Schema.Ohlcv1M,
            "ohlcv-1h" => Schema.Ohlcv1H,
            "ohlcv-1d" => Schema.Ohlcv1D,
            "definition" => Schema.Definition,
            "statistics" => Schema.Statistics,
            "status" => Schema.Status,
            "imbalance" => Schema.Imbalance,
            "ohlcv-eod" => Schema.OhlcvEod,
            "cmbp-1" => Schema.Cmbp1,
            "cbbo-1s" => Schema.Cbbo1S,
            "cbbo-1m" => Schema.Cbbo1M,
            "tcbbo" => Schema.Tcbbo,
            "bbo-1s" => Schema.Bbo1S,
            "bbo-1m" => Schema.Bbo1M,
            _ => throw new ArgumentException($"Unknown schema: {schemaString}", nameof(schemaString))
        };
    }
}
