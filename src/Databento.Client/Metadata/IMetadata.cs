namespace Databento.Client.Metadata;

/// <summary>
/// Metadata interface for querying instrument information
/// </summary>
public interface IMetadata : IDisposable
{
    /// <summary>
    /// Get symbol string for an instrument ID
    /// </summary>
    /// <param name="instrumentId">Instrument ID to look up</param>
    /// <returns>Symbol string, or null if not found</returns>
    string? GetSymbol(uint instrumentId);

    /// <summary>
    /// Check if metadata contains mapping for instrument ID
    /// </summary>
    /// <param name="instrumentId">Instrument ID to check</param>
    /// <returns>True if mapping exists</returns>
    bool Contains(uint instrumentId);

    /// <summary>
    /// Create a timeseries symbol map from this metadata.
    /// Useful for working with historical data where symbols may change over time.
    /// </summary>
    /// <returns>Timeseries symbol map</returns>
    ITsSymbolMap CreateSymbolMap();

    /// <summary>
    /// Create a point-in-time symbol map for a specific date from this metadata.
    /// Useful for working with live data or historical requests over a single day.
    /// </summary>
    /// <param name="date">The date for the symbol mappings</param>
    /// <returns>Point-in-time symbol map</returns>
    IPitSymbolMap CreateSymbolMapForDate(DateOnly date);
}
