using System.Text.Json.Serialization;

namespace Databento.Client.Models.Reference;

/// <summary>
/// Represents an adjustment factor record for corporate actions
/// </summary>
public sealed class AdjustmentFactorRecord
{
    /// <summary>Ex-date of the corporate action</summary>
    [JsonPropertyName("ex_date")]
    public DateOnly ExDate { get; set; }

    /// <summary>Security ID</summary>
    [JsonPropertyName("security_id")]
    public string? SecurityId { get; set; }

    /// <summary>Event ID</summary>
    [JsonPropertyName("event_id")]
    public string? EventId { get; set; }

    /// <summary>Event type (DIV, SPLT, etc.)</summary>
    [JsonPropertyName("event")]
    public string? Event { get; set; }

    /// <summary>Issuer name</summary>
    [JsonPropertyName("issuer_name")]
    public string? IssuerName { get; set; }

    /// <summary>Security type</summary>
    [JsonPropertyName("security_type")]
    public string? SecurityType { get; set; }

    /// <summary>Adjustment factor for split adjustments</summary>
    [JsonPropertyName("split_adjustment_factor")]
    public decimal? SplitAdjustmentFactor { get; set; }

    /// <summary>Adjustment factor for price adjustments</summary>
    [JsonPropertyName("price_adjustment_factor")]
    public decimal? PriceAdjustmentFactor { get; set; }

    /// <summary>Cumulative split adjustment factor</summary>
    [JsonPropertyName("cumulative_split_adjustment_factor")]
    public decimal? CumulativeSplitAdjustmentFactor { get; set; }

    /// <summary>Cumulative price adjustment factor</summary>
    [JsonPropertyName("cumulative_price_adjustment_factor")]
    public decimal? CumulativePriceAdjustmentFactor { get; set; }

    /// <summary>Dividend amount</summary>
    [JsonPropertyName("dividend_amount")]
    public decimal? DividendAmount { get; set; }

    /// <summary>Dividend currency</summary>
    [JsonPropertyName("dividend_currency")]
    public string? DividendCurrency { get; set; }

    /// <summary>Dividend frequency (INT, FNL, etc.)</summary>
    [JsonPropertyName("frequency")]
    public string? Frequency { get; set; }

    /// <summary>Option flag</summary>
    [JsonPropertyName("option")]
    public int? Option { get; set; }

    /// <summary>Event detail description</summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    /// <summary>Timestamp when record was created</summary>
    [JsonPropertyName("ts_created")]
    public DateTimeOffset TsCreated { get; set; }
}
