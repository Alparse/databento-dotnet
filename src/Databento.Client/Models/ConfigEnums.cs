namespace Databento.Client.Models;

/// <summary>
/// Historical API gateway selection
/// </summary>
public enum HistoricalGateway
{
    /// <summary>Primary gateway (bo1.databento.com)</summary>
    Bo1,

    /// <summary>Secondary gateway (bo2.databento.com)</summary>
    Bo2,

    /// <summary>Custom gateway address</summary>
    Custom
}

/// <summary>
/// Live feed mode
/// </summary>
public enum FeedMode
{
    /// <summary>Live streaming data</summary>
    Live,

    /// <summary>Live data with initial snapshot</summary>
    LiveSnapshot,

    /// <summary>Historical replay mode</summary>
    Historical
}

/// <summary>
/// Time duration for splitting batch output files.
/// Numeric values are the wire contract with the native wrapper
/// (see dbento_batch_submit_job_ex in databento_native.h); do not reorder.
/// </summary>
public enum SplitDuration
{
    /// <summary>No splitting - single file</summary>
    None = 0,

    /// <summary>Split by day</summary>
    Day = 1,

    /// <summary>Split by week</summary>
    Week = 2,

    /// <summary>Split by month</summary>
    Month = 3,

    /// <summary>Split by year</summary>
    Year = 4
}

/// <summary>
/// Batch job delivery method
/// </summary>
public enum Delivery
{
    /// <summary>Direct download</summary>
    Download,

    /// <summary>AWS S3 delivery</summary>
    S3,

    /// <summary>Write to local disk</summary>
    Disk
}

/// <summary>
/// Data encoding format
/// </summary>
public enum Encoding
{
    /// <summary>Databento Binary Encoding (DBN)</summary>
    Dbn,

    /// <summary>Comma-separated values</summary>
    Csv,

    /// <summary>JSON format</summary>
    Json
}

/// <summary>
/// Compression algorithm
/// </summary>
public enum Compression
{
    /// <summary>No compression</summary>
    None,

    /// <summary>Zstandard compression</summary>
    Zstd,

    /// <summary>Gzip compression</summary>
    Gzip
}

/// <summary>
/// DBN version upgrade policy
/// </summary>
public enum VersionUpgradePolicy
{
    /// <summary>Keep data in original DBN version</summary>
    AsIs,

    /// <summary>Upgrade to latest DBN version</summary>
    Upgrade
}

/// <summary>
/// Batch job state
/// </summary>
public enum JobState
{
    /// <summary>Job queued for processing</summary>
    Queued = 0,

    /// <summary>Job currently processing</summary>
    Processing = 1,

    /// <summary>Job completed successfully</summary>
    Done = 2,

    /// <summary>Job expired before completion</summary>
    Expired = 3
}

/// <summary>
/// Dataset availability condition
/// </summary>
public enum DatasetCondition
{
    /// <summary>Dataset is available</summary>
    Available,

    /// <summary>Dataset has degraded availability</summary>
    Degraded,

    /// <summary>Dataset availability is pending</summary>
    Pending,

    /// <summary>Dataset is missing or unavailable</summary>
    Missing
}

/// <summary>
/// Extension methods for configuration enums
/// </summary>
public static class ConfigEnumExtensions
{
    /// <summary>
    /// Convert HistoricalGateway to string
    /// </summary>
    public static string ToGatewayString(this HistoricalGateway gateway)
    {
        return gateway switch
        {
            HistoricalGateway.Bo1 => "bo1",
            HistoricalGateway.Bo2 => "bo2",
            HistoricalGateway.Custom => "custom",
            _ => throw new ArgumentOutOfRangeException(nameof(gateway))
        };
    }

    /// <summary>
    /// Parse HistoricalGateway from string
    /// </summary>
    public static HistoricalGateway ParseGateway(string gatewayString)
    {
        return gatewayString.ToLowerInvariant() switch
        {
            "bo1" => HistoricalGateway.Bo1,
            "bo2" => HistoricalGateway.Bo2,
            "custom" => HistoricalGateway.Custom,
            _ => throw new ArgumentException($"Unknown gateway: {gatewayString}", nameof(gatewayString))
        };
    }

    /// <summary>
    /// Convert Encoding to string
    /// </summary>
    public static string ToEncodingString(this Encoding encoding)
    {
        return encoding switch
        {
            Encoding.Dbn => "dbn",
            Encoding.Csv => "csv",
            Encoding.Json => "json",
            _ => throw new ArgumentOutOfRangeException(nameof(encoding))
        };
    }

    /// <summary>
    /// Parse Encoding from string
    /// </summary>
    public static Encoding ParseEncoding(string encodingString)
    {
        return encodingString.ToLowerInvariant() switch
        {
            "dbn" => Encoding.Dbn,
            "csv" => Encoding.Csv,
            "json" => Encoding.Json,
            _ => throw new ArgumentException($"Unknown encoding: {encodingString}", nameof(encodingString))
        };
    }

    /// <summary>
    /// Convert Compression to string
    /// </summary>
    public static string ToCompressionString(this Compression compression)
    {
        return compression switch
        {
            Compression.None => "none",
            Compression.Zstd => "zstd",
            Compression.Gzip => "gzip",
            _ => throw new ArgumentOutOfRangeException(nameof(compression))
        };
    }

    /// <summary>
    /// Parse Compression from string
    /// </summary>
    public static Compression ParseCompression(string compressionString)
    {
        return compressionString.ToLowerInvariant() switch
        {
            "none" => Compression.None,
            "zstd" => Compression.Zstd,
            "gzip" => Compression.Gzip,
            _ => throw new ArgumentException($"Unknown compression: {compressionString}", nameof(compressionString))
        };
    }

    /// <summary>
    /// Convert SplitDuration to string
    /// </summary>
    public static string ToSplitDurationString(this SplitDuration duration)
    {
        return duration switch
        {
            SplitDuration.None => "none",
            SplitDuration.Day => "day",
            SplitDuration.Week => "week",
            SplitDuration.Month => "month",
            SplitDuration.Year => "year",
            _ => throw new ArgumentOutOfRangeException(nameof(duration))
        };
    }

    /// <summary>
    /// Parse SplitDuration from string
    /// </summary>
    public static SplitDuration ParseSplitDuration(string durationString)
    {
        return durationString.ToLowerInvariant() switch
        {
            "none" => SplitDuration.None,
            "day" => SplitDuration.Day,
            "week" => SplitDuration.Week,
            "month" => SplitDuration.Month,
            "year" => SplitDuration.Year,
            _ => throw new ArgumentException($"Unknown split duration: {durationString}", nameof(durationString))
        };
    }

    /// <summary>
    /// Convert JobState to string
    /// </summary>
    public static string ToJobStateString(this JobState state)
    {
        return state switch
        {
            JobState.Queued => "queued",
            JobState.Processing => "processing",
            JobState.Done => "done",
            JobState.Expired => "expired",
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
    }

    /// <summary>
    /// Parse JobState from string
    /// </summary>
    public static JobState ParseJobState(string stateString)
    {
        return stateString.ToLowerInvariant() switch
        {
            "queued" => JobState.Queued,
            "processing" => JobState.Processing,
            "done" => JobState.Done,
            "expired" => JobState.Expired,
            _ => throw new ArgumentException($"Unknown job state: {stateString}", nameof(stateString))
        };
    }

    /// <summary>
    /// Convert DatasetCondition to string
    /// </summary>
    public static string ToConditionString(this DatasetCondition condition)
    {
        return condition switch
        {
            DatasetCondition.Available => "available",
            DatasetCondition.Degraded => "degraded",
            DatasetCondition.Pending => "pending",
            DatasetCondition.Missing => "missing",
            _ => throw new ArgumentOutOfRangeException(nameof(condition))
        };
    }

    /// <summary>
    /// Parse DatasetCondition from string
    /// </summary>
    public static DatasetCondition ParseDatasetCondition(string conditionString)
    {
        return conditionString.ToLowerInvariant() switch
        {
            "available" => DatasetCondition.Available,
            "degraded" => DatasetCondition.Degraded,
            "pending" => DatasetCondition.Pending,
            "missing" => DatasetCondition.Missing,
            _ => throw new ArgumentException($"Unknown dataset condition: {conditionString}", nameof(conditionString))
        };
    }
}
