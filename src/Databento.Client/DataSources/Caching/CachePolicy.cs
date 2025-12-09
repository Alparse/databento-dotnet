namespace Databento.Client.DataSources.Caching;

/// <summary>
/// Specifies how historical data should be cached during backtesting.
/// </summary>
public enum CachePolicy
{
    /// <summary>
    /// No caching - always fetch from API.
    /// Use this for one-shot analysis or when disk space is limited.
    /// </summary>
    None,

    /// <summary>
    /// Cache in memory only.
    /// Fast replay, but limited by available RAM and lost on process exit.
    /// </summary>
    Memory,

    /// <summary>
    /// Cache to disk as DBN files.
    /// Persists across process restarts, unlimited replay.
    /// </summary>
    Disk
}
