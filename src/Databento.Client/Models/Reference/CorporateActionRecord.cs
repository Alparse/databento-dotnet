using System.Text.Json.Serialization;

namespace Databento.Client.Models.Reference;

/// <summary>
/// Represents a corporate action record
/// </summary>
public sealed class CorporateActionRecord
{
    // Index fields
    /// <summary>Event date (used as index by default)</summary>
    [JsonPropertyName("event_date")]
    public DateOnly? EventDate { get; set; }

    /// <summary>Ex-date of the corporate action</summary>
    [JsonPropertyName("ex_date")]
    public DateOnly? ExDate { get; set; }

    /// <summary>Record timestamp</summary>
    [JsonPropertyName("ts_record")]
    public DateTimeOffset TsRecord { get; set; }

    // Event identifiers
    /// <summary>Unique event ID</summary>
    [JsonPropertyName("event_unique_id")]
    public string? EventUniqueId { get; set; }

    /// <summary>Event ID</summary>
    [JsonPropertyName("event_id")]
    public string? EventId { get; set; }

    /// <summary>Related event ID</summary>
    [JsonPropertyName("related_event_id")]
    public string? RelatedEventId { get; set; }

    /// <summary>Related event type</summary>
    [JsonPropertyName("related_event")]
    public string? RelatedEvent { get; set; }

    // Listing and security IDs
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

    // Event information
    /// <summary>Event action (I=Insert, U=Update, etc.)</summary>
    [JsonPropertyName("event_action")]
    public string? EventAction { get; set; }

    /// <summary>Event type (SHOCH, DIV, SPLT, etc.)</summary>
    [JsonPropertyName("event")]
    public string? Event { get; set; }

    /// <summary>Event subtype</summary>
    [JsonPropertyName("event_subtype")]
    public string? EventSubtype { get; set; }

    /// <summary>Event date label</summary>
    [JsonPropertyName("event_date_label")]
    public string? EventDateLabel { get; set; }

    /// <summary>Event created date</summary>
    [JsonPropertyName("event_created_date")]
    public DateOnly? EventCreatedDate { get; set; }

    // Status fields
    /// <summary>Global status (A=Active, etc.)</summary>
    [JsonPropertyName("global_status")]
    public string? GlobalStatus { get; set; }

    /// <summary>Listing status</summary>
    [JsonPropertyName("listing_status")]
    public string? ListingStatus { get; set; }

    /// <summary>Listing source</summary>
    [JsonPropertyName("listing_source")]
    public string? ListingSource { get; set; }

    // Dates
    /// <summary>Effective date</summary>
    [JsonPropertyName("effective_date")]
    public DateOnly? EffectiveDate { get; set; }

    /// <summary>Record date</summary>
    [JsonPropertyName("record_date")]
    public DateOnly? RecordDate { get; set; }

    /// <summary>Record date ID</summary>
    [JsonPropertyName("record_date_id")]
    public string? RecordDateId { get; set; }

    /// <summary>Payment date</summary>
    [JsonPropertyName("payment_date")]
    public DateOnly? PaymentDate { get; set; }

    /// <summary>Duebills redemption date</summary>
    [JsonPropertyName("duebills_redemption_date")]
    public DateOnly? DuebillsRedemptionDate { get; set; }

    /// <summary>From date</summary>
    [JsonPropertyName("from_date")]
    public DateOnly? FromDate { get; set; }

    /// <summary>To date</summary>
    [JsonPropertyName("to_date")]
    public DateOnly? ToDate { get; set; }

    /// <summary>Registration date</summary>
    [JsonPropertyName("registration_date")]
    public DateOnly? RegistrationDate { get; set; }

    /// <summary>Start date</summary>
    [JsonPropertyName("start_date")]
    public DateOnly? StartDate { get; set; }

    /// <summary>End date</summary>
    [JsonPropertyName("end_date")]
    public DateOnly? EndDate { get; set; }

    /// <summary>Open date</summary>
    [JsonPropertyName("open_date")]
    public DateOnly? OpenDate { get; set; }

    /// <summary>Close date</summary>
    [JsonPropertyName("close_date")]
    public DateOnly? CloseDate { get; set; }

    /// <summary>Start subscription date</summary>
    [JsonPropertyName("start_subscription_date")]
    public DateOnly? StartSubscriptionDate { get; set; }

    /// <summary>End subscription date</summary>
    [JsonPropertyName("end_subscription_date")]
    public DateOnly? EndSubscriptionDate { get; set; }

    /// <summary>Option election date</summary>
    [JsonPropertyName("option_election_date")]
    public DateOnly? OptionElectionDate { get; set; }

    /// <summary>Withdrawal rights from date</summary>
    [JsonPropertyName("withdrawal_rights_from_date")]
    public DateOnly? WithdrawalRightsFromDate { get; set; }

    /// <summary>Withdrawal rights to date</summary>
    [JsonPropertyName("withdrawal_rights_to_date")]
    public DateOnly? WithdrawalRightsToDate { get; set; }

    /// <summary>Notification date</summary>
    [JsonPropertyName("notification_date")]
    public DateOnly? NotificationDate { get; set; }

    /// <summary>Financial year end date</summary>
    [JsonPropertyName("financial_year_end_date")]
    public DateOnly? FinancialYearEndDate { get; set; }

    /// <summary>Expected completion date</summary>
    [JsonPropertyName("exp_completion_date")]
    public DateOnly? ExpCompletionDate { get; set; }

    /// <summary>Listing date</summary>
    [JsonPropertyName("listing_date")]
    public DateOnly? ListingDate { get; set; }

    /// <summary>Delisting date</summary>
    [JsonPropertyName("delisting_date")]
    public DateOnly? DelistingDate { get; set; }

    // Issuer and security information
    /// <summary>Issuer name</summary>
    [JsonPropertyName("issuer_name")]
    public string? IssuerName { get; set; }

    /// <summary>Security type</summary>
    [JsonPropertyName("security_type")]
    public string? SecurityType { get; set; }

    /// <summary>Security description</summary>
    [JsonPropertyName("security_description")]
    public string? SecurityDescription { get; set; }

    // Exchange information
    /// <summary>Primary exchange</summary>
    [JsonPropertyName("primary_exchange")]
    public string? PrimaryExchange { get; set; }

    /// <summary>Exchange</summary>
    [JsonPropertyName("exchange")]
    public string? Exchange { get; set; }

    /// <summary>Operating MIC code</summary>
    [JsonPropertyName("operating_mic")]
    public string? OperatingMic { get; set; }

    /// <summary>Segment MIC name</summary>
    [JsonPropertyName("segment_mic_name")]
    public string? SegmentMicName { get; set; }

    /// <summary>Segment MIC code</summary>
    [JsonPropertyName("segment_mic")]
    public string? SegmentMic { get; set; }

    // Symbology
    /// <summary>Symbol</summary>
    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    /// <summary>Nasdaq symbol (standardized raw_symbol)</summary>
    [JsonPropertyName("nasdaq_symbol")]
    public string? NasdaqSymbol { get; set; }

    /// <summary>Local code</summary>
    [JsonPropertyName("local_code")]
    public string? LocalCode { get; set; }

    /// <summary>ISIN</summary>
    [JsonPropertyName("isin")]
    public string? Isin { get; set; }

    /// <summary>US CUSIP code</summary>
    [JsonPropertyName("us_code")]
    public string? UsCode { get; set; }

    /// <summary>Bloomberg composite ID</summary>
    [JsonPropertyName("bbg_comp_id")]
    public string? BbgCompId { get; set; }

    /// <summary>Bloomberg composite ticker</summary>
    [JsonPropertyName("bbg_comp_ticker")]
    public string? BbgCompTicker { get; set; }

    /// <summary>FIGI</summary>
    [JsonPropertyName("figi")]
    public string? Figi { get; set; }

    /// <summary>FIGI ticker</summary>
    [JsonPropertyName("figi_ticker")]
    public string? FigiTicker { get; set; }

    // Country and currency
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

    /// <summary>Rate currency</summary>
    [JsonPropertyName("rate_currency")]
    public string? RateCurrency { get; set; }

    /// <summary>Par value currency</summary>
    [JsonPropertyName("par_value_currency")]
    public string? ParValueCurrency { get; set; }

    // Security attributes
    /// <summary>Mandatory/voluntary flag</summary>
    [JsonPropertyName("mand_volu_flag")]
    public string? MandVoluFlag { get; set; }

    /// <summary>RD priority</summary>
    [JsonPropertyName("rd_priority")]
    public string? RdPriority { get; set; }

    /// <summary>Lot size</summary>
    [JsonPropertyName("lot_size")]
    public int? LotSize { get; set; }

    /// <summary>Par value</summary>
    [JsonPropertyName("par_value")]
    public decimal? ParValue { get; set; }

    // Rate and ratio information
    /// <summary>Payment type</summary>
    [JsonPropertyName("payment_type")]
    public string? PaymentType { get; set; }

    /// <summary>Option ID</summary>
    [JsonPropertyName("option_id")]
    public string? OptionId { get; set; }

    /// <summary>Serial ID</summary>
    [JsonPropertyName("serial_id")]
    public string? SerialId { get; set; }

    /// <summary>Default option flag</summary>
    [JsonPropertyName("default_option_flag")]
    public bool? DefaultOptionFlag { get; set; }

    /// <summary>Ratio old</summary>
    [JsonPropertyName("ratio_old")]
    public decimal? RatioOld { get; set; }

    /// <summary>Ratio new</summary>
    [JsonPropertyName("ratio_new")]
    public decimal? RatioNew { get; set; }

    /// <summary>Fraction</summary>
    [JsonPropertyName("fraction")]
    public decimal? Fraction { get; set; }

    // Outturn information
    /// <summary>Outturn style</summary>
    [JsonPropertyName("outturn_style")]
    public string? OutturnStyle { get; set; }

    /// <summary>Outturn security type</summary>
    [JsonPropertyName("outturn_security_type")]
    public string? OutturnSecurityType { get; set; }

    /// <summary>Outturn security ID</summary>
    [JsonPropertyName("outturn_security_id")]
    public string? OutturnSecurityId { get; set; }

    /// <summary>Outturn ISIN</summary>
    [JsonPropertyName("outturn_isin")]
    public string? OutturnIsin { get; set; }

    /// <summary>Outturn US code</summary>
    [JsonPropertyName("outturn_us_code")]
    public string? OutturnUsCode { get; set; }

    /// <summary>Outturn local code</summary>
    [JsonPropertyName("outturn_local_code")]
    public string? OutturnLocalCode { get; set; }

    /// <summary>Outturn Bloomberg composite ID</summary>
    [JsonPropertyName("outturn_bbg_comp_id")]
    public string? OutturnBbgCompId { get; set; }

    /// <summary>Outturn Bloomberg composite ticker</summary>
    [JsonPropertyName("outturn_bbg_comp_ticker")]
    public string? OutturnBbgCompTicker { get; set; }

    /// <summary>Outturn FIGI</summary>
    [JsonPropertyName("outturn_figi")]
    public string? OutturnFigi { get; set; }

    /// <summary>Outturn FIGI ticker</summary>
    [JsonPropertyName("outturn_figi_ticker")]
    public string? OutturnFigiTicker { get; set; }

    // Quantity limits
    /// <summary>Minimum offer quantity</summary>
    [JsonPropertyName("min_offer_qty")]
    public long? MinOfferQty { get; set; }

    /// <summary>Maximum offer quantity</summary>
    [JsonPropertyName("max_offer_qty")]
    public long? MaxOfferQty { get; set; }

    /// <summary>Minimum qualify quantity</summary>
    [JsonPropertyName("min_qualify_qty")]
    public long? MinQualifyQty { get; set; }

    /// <summary>Maximum qualify quantity</summary>
    [JsonPropertyName("max_qualify_qty")]
    public long? MaxQualifyQty { get; set; }

    /// <summary>Minimum accept quantity</summary>
    [JsonPropertyName("min_accept_qty")]
    public long? MinAcceptQty { get; set; }

    /// <summary>Maximum accept quantity</summary>
    [JsonPropertyName("max_accept_qty")]
    public long? MaxAcceptQty { get; set; }

    // Pricing
    /// <summary>Tender strike price</summary>
    [JsonPropertyName("tender_strike_price")]
    public decimal? TenderStrikePrice { get; set; }

    /// <summary>Tender price step</summary>
    [JsonPropertyName("tender_price_step")]
    public decimal? TenderPriceStep { get; set; }

    // Option and withdrawal information
    /// <summary>Option expiry time</summary>
    [JsonPropertyName("option_expiry_time")]
    public DateTimeOffset? OptionExpiryTime { get; set; }

    /// <summary>Option expiry timezone</summary>
    [JsonPropertyName("option_expiry_tz")]
    public string? OptionExpiryTz { get; set; }

    /// <summary>Withdrawal rights flag</summary>
    [JsonPropertyName("withdrawal_rights_flag")]
    public bool? WithdrawalRightsFlag { get; set; }

    /// <summary>Withdrawal rights expiry time</summary>
    [JsonPropertyName("withdrawal_rights_expiry_time")]
    public DateTimeOffset? WithdrawalRightsExpiryTime { get; set; }

    /// <summary>Withdrawal rights expiry timezone</summary>
    [JsonPropertyName("withdrawal_rights_expiry_tz")]
    public string? WithdrawalRightsExpiryTz { get; set; }

    /// <summary>Expiry time</summary>
    [JsonPropertyName("expiry_time")]
    public DateTimeOffset? ExpiryTime { get; set; }

    /// <summary>Expiry timezone</summary>
    [JsonPropertyName("expiry_tz")]
    public string? ExpiryTz { get; set; }

    // Shares outstanding information
    /// <summary>Old outstanding date</summary>
    [JsonPropertyName("old_outstanding_date")]
    public DateTimeOffset? OldOutstandingDate { get; set; }

    /// <summary>New outstanding date</summary>
    [JsonPropertyName("new_outstanding_date")]
    public DateTimeOffset? NewOutstandingDate { get; set; }

    /// <summary>Old shares outstanding</summary>
    [JsonPropertyName("old_shares_outstanding")]
    public long? OldSharesOutstanding { get; set; }

    /// <summary>New shares outstanding</summary>
    [JsonPropertyName("new_shares_outstanding")]
    public long? NewSharesOutstanding { get; set; }

    /// <summary>Timestamp when record was created</summary>
    [JsonPropertyName("ts_created")]
    public DateTimeOffset TsCreated { get; set; }
}
