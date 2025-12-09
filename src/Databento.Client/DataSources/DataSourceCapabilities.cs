namespace Databento.Client.DataSources;

/// <summary>
/// Describes the capabilities of a data source.
/// Used to determine what operations are supported by the current source.
/// </summary>
public sealed record DataSourceCapabilities
{
    /// <summary>
    /// Whether the source can recover from disconnection.
    /// True for live gateway and cached historical sources.
    /// </summary>
    public bool SupportsReconnect { get; init; }

    /// <summary>
    /// Whether the source can request MBO (market-by-order) snapshot at subscription start.
    /// Only true for live gateway.
    /// </summary>
    public bool SupportsSnapshot { get; init; }

    /// <summary>
    /// Whether the source can replay data from the beginning.
    /// True for historical and file sources.
    /// </summary>
    public bool SupportsReplay { get; init; }

    /// <summary>
    /// Whether data arrives in real-time.
    /// Only true for live gateway.
    /// </summary>
    public bool IsRealTime { get; init; }

    /// <summary>
    /// Whether playback speed can be controlled.
    /// True for historical and file sources.
    /// </summary>
    public bool SupportsPlaybackSpeed { get; init; }

    /// <summary>
    /// Whether streaming can be paused and resumed.
    /// True for historical and file sources.
    /// </summary>
    public bool SupportsPauseResume { get; init; }

    /// <summary>
    /// Capabilities for live data sources (gateway connection).
    /// </summary>
    public static DataSourceCapabilities Live => new()
    {
        SupportsReconnect = true,
        SupportsSnapshot = true,
        SupportsReplay = false,  // Only intraday replay, not full replay
        IsRealTime = true,
        SupportsPlaybackSpeed = false,
        SupportsPauseResume = false
    };

    /// <summary>
    /// Capabilities for historical data sources (HTTP API).
    /// </summary>
    public static DataSourceCapabilities Historical => new()
    {
        SupportsReconnect = false,  // True only if cached
        SupportsSnapshot = false,
        SupportsReplay = true,
        IsRealTime = false,
        SupportsPlaybackSpeed = true,
        SupportsPauseResume = true
    };

    /// <summary>
    /// Capabilities for historical data sources with caching enabled.
    /// </summary>
    public static DataSourceCapabilities HistoricalCached => new()
    {
        SupportsReconnect = true,  // Can reconnect because data is cached
        SupportsSnapshot = false,
        SupportsReplay = true,
        IsRealTime = false,
        SupportsPlaybackSpeed = true,
        SupportsPauseResume = true
    };

    /// <summary>
    /// Capabilities for file-based data sources (DBN files).
    /// </summary>
    public static DataSourceCapabilities File => new()
    {
        SupportsReconnect = true,  // Can re-read file
        SupportsSnapshot = false,
        SupportsReplay = true,
        IsRealTime = false,
        SupportsPlaybackSpeed = true,
        SupportsPauseResume = true
    };
}
