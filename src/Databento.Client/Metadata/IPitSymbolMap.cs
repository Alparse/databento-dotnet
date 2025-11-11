using Databento.Client.Models;

namespace Databento.Client.Metadata;

/// <summary>
/// Point-in-time symbol map for resolving instrument IDs to symbols at a specific moment.
/// Useful for working with live data or historical requests over a single day where
/// symbol mappings are known not to change.
/// </summary>
public interface IPitSymbolMap : IDisposable
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
    /// Find symbol for an instrument ID
    /// </summary>
    /// <param name="instrumentId">The instrument ID</param>
    /// <returns>Symbol string if found, null otherwise</returns>
    string? Find(uint instrumentId);

    /// <summary>
    /// Get symbol for an instrument ID (throws if not found)
    /// </summary>
    /// <param name="instrumentId">The instrument ID</param>
    /// <returns>Symbol string</returns>
    /// <exception cref="KeyNotFoundException">If mapping not found</exception>
    string At(uint instrumentId);

    /// <summary>
    /// Update symbol map from a record (for live data)
    /// </summary>
    /// <param name="record">Record containing symbol mapping information</param>
    void OnRecord(Record record);
}
