namespace Databento.Client.Metadata;

/// <summary>
/// Timeseries symbol map for resolving instrument IDs to symbols across time.
/// Useful for working with historical data where symbols may change over time.
/// </summary>
public interface ITsSymbolMap : IDisposable
{
    /// <summary>
    /// Whether the symbol map is empty
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Number of mappings in the symbol map
    /// </summary>
    int Size { get; }

    /// <summary>
    /// Find symbol for an instrument ID on a specific date
    /// </summary>
    /// <param name="date">The date to look up</param>
    /// <param name="instrumentId">The instrument ID</param>
    /// <returns>Symbol string if found, null otherwise</returns>
    string? Find(DateOnly date, uint instrumentId);

    /// <summary>
    /// Get symbol for an instrument ID on a specific date (throws if not found)
    /// </summary>
    /// <param name="date">The date to look up</param>
    /// <param name="instrumentId">The instrument ID</param>
    /// <returns>Symbol string</returns>
    /// <exception cref="KeyNotFoundException">If mapping not found</exception>
    string At(DateOnly date, uint instrumentId);
}
