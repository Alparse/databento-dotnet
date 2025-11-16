using System.Text.Json.Serialization;

namespace Databento.Client.Models.Reference;

/// <summary>
/// Represents a security master record with listing and security information
/// </summary>
public sealed class SecurityMasterRecord
{
    /// <summary>Effective date of the record (timestamp with timezone)</summary>
    [JsonPropertyName("ts_effective")]
    public DateTimeOffset TsEffective { get; set; }

    /// <summary>Record timestamp (timestamp with timezone)</summary>
    [JsonPropertyName("ts_record")]
    public DateTimeOffset TsRecord { get; set; }

    /// <summary>Listing ID</summary>
    [JsonPropertyName("listing_id")]
    public string? ListingId { get; set; }

    /// <summary>Listing group ID</summary>
    [JsonPropertyName("listing_group_id")]
    public string? ListingGroupId { get; set; }

    /// <summary>Security ID</summary>
    [JsonPropertyName("security_id")]
    public string? SecurityId { get; set; }

    /// <summary>Issuer ID</summary>
    [JsonPropertyName("issuer_id")]
    public string? IssuerId { get; set; }

    /// <summary>Listing status (A=Active, L=Listed, etc.)</summary>
    [JsonPropertyName("listing_status")]
    public string? ListingStatus { get; set; }

    /// <summary>Listing source</summary>
    [JsonPropertyName("listing_source")]
    public string? ListingSource { get; set; }

    /// <summary>Listing created date</summary>
    [JsonPropertyName("listing_created_date")]
    public DateOnly? ListingCreatedDate { get; set; }

    /// <summary>Listing date</summary>
    [JsonPropertyName("listing_date")]
    public DateOnly? ListingDate { get; set; }

    /// <summary>Delisting date (null if still listed)</summary>
    [JsonPropertyName("delisting_date")]
    public DateOnly? DelistingDate { get; set; }

    /// <summary>Issuer name</summary>
    [JsonPropertyName("issuer_name")]
    public string? IssuerName { get; set; }

    /// <summary>Security type (e.g., "EQS" for Ordinary Shares)</summary>
    [JsonPropertyName("security_type")]
    public string? SecurityType { get; set; }

    /// <summary>Security description</summary>
    [JsonPropertyName("security_description")]
    public string? SecurityDescription { get; set; }

    /// <summary>Primary exchange</summary>
    [JsonPropertyName("primary_exchange")]
    public string? PrimaryExchange { get; set; }

    /// <summary>Exchange</summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; set; }

    /// <summary>Operating MIC code</summary>
    [JsonPropertyName("operating_mic")]
    public string? OperatingMic { get; set; }

    /// <summary>Symbol</summary>
    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    /// <summary>Nasdaq Integrated Platform Suffix convention symbol (standardized raw_symbol)</summary>
    [JsonPropertyName("nasdaq_symbol")]
    public string? NasdaqSymbol { get; set; }

    /// <summary>Local code (original symbol from primary exchange)</summary>
    [JsonPropertyName("local_code")]
    public string? LocalCode { get; set; }

    /// <summary>ISIN (International Securities Identification Number)</summary>
    [JsonPropertyName("isin")]
    public string? Isin { get; set; }

    /// <summary>US CUSIP code</summary>
    [JsonPropertyName("us_code")]
    public string? UsCode { get; set; }

    /// <summary>Bloomberg composite global ID</summary>
    [JsonPropertyName("bbg_comp_id")]
    public string? BbgCompId { get; set; }

    /// <summary>Bloomberg composite ticker</summary>
    [JsonPropertyName("bbg_comp_ticker")]
    public string? BbgCompTicker { get; set; }

    /// <summary>FIGI (Financial Instrument Global Identifier)</summary>
    [JsonPropertyName("figi")]
    public string? Figi { get; set; }

    /// <summary>FIGI ticker</summary>
    [JsonPropertyName("figi_ticker")]
    public string? FigiTicker { get; set; }

    /// <summary>FISN (Financial Instrument Short Name)</summary>
    [JsonPropertyName("fisn")]
    public string? Fisn { get; set; }

    /// <summary>LEI (Legal Entity Identifier)</summary>
    [JsonPropertyName("lei")]
    public string? Lei { get; set; }

    /// <summary>SIC code</summary>
    [JsonPropertyName("sic")]
    public string? Sic { get; set; }

    /// <summary>CIK (Central Index Key)</summary>
    [JsonPropertyName("cik")]
    public string? Cik { get; set; }

    /// <summary>GICS code</summary>
    [JsonPropertyName("gics")]
    public string? Gics { get; set; }

    /// <summary>NAICS code</summary>
    [JsonPropertyName("naics")]
    public string? Naics { get; set; }

    /// <summary>CIC code</summary>
    [JsonPropertyName("cic")]
    public string? Cic { get; set; }

    /// <summary>CFI code</summary>
    [JsonPropertyName("cfi")]
    public string? Cfi { get; set; }

    /// <summary>Incorporation country (ISO 3166-1 alpha-2)</summary>
    [JsonPropertyName("incorporation_country")]
    public string? IncorporationCountry { get; set; }

    /// <summary>Listing country (ISO 3166-1 alpha-2)</summary>
    [JsonPropertyName("listing_country")]
    public string? ListingCountry { get; set; }

    /// <summary>Register country (ISO 3166-1 alpha-2)</summary>
    [JsonPropertyName("register_country")]
    public string? RegisterCountry { get; set; }

    /// <summary>Trading currency (ISO 4217)</summary>
    [JsonPropertyName("trading_currency")]
    public string? TradingCurrency { get; set; }

    /// <summary>Multi-currency flag</summary>
    [JsonPropertyName("multi_currency")]
    public bool? MultiCurrency { get; set; }

    /// <summary>Segment MIC name</summary>
    [JsonPropertyName("segment_mic_name")]
    public string? SegmentMicName { get; set; }

    /// <summary>Segment MIC code</summary>
    [JsonPropertyName("segment_mic")]
    public string? SegmentMic { get; set; }

    /// <summary>Structure</summary>
    [JsonPropertyName("structure")]
    public string? Structure { get; set; }

    /// <summary>Lot size</summary>
    [JsonPropertyName("lot_size")]
    public int? LotSize { get; set; }

    /// <summary>Par value</summary>
    [JsonPropertyName("par_value")]
    public decimal? ParValue { get; set; }

    /// <summary>Par value currency</summary>
    [JsonPropertyName("par_value_currency")]
    public string? ParValueCurrency { get; set; }

    /// <summary>Voting rights (V=Voting, N=Non-voting)</summary>
    [JsonPropertyName("voting")]
    public string? Voting { get; set; }

    /// <summary>Votes per security</summary>
    [JsonPropertyName("vote_per_sec")]
    public int? VotePerSec { get; set; }

    /// <summary>Shares outstanding</summary>
    [JsonPropertyName("shares_outstanding")]
    public long? SharesOutstanding { get; set; }

    /// <summary>Shares outstanding date</summary>
    [JsonPropertyName("shares_outstanding_date")]
    public DateOnly? SharesOutstandingDate { get; set; }

    /// <summary>Timestamp when record was created</summary>
    [JsonPropertyName("ts_created")]
    public DateTimeOffset TsCreated { get; set; }
}
