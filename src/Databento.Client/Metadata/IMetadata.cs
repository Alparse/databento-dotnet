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
}
