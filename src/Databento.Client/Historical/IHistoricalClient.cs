using Databento.Client.Metadata;
using Databento.Client.Models;

namespace Databento.Client.Historical;

/// <summary>
/// Historical data client for querying past market data
/// </summary>
public interface IHistoricalClient : IAsyncDisposable
{
    /// <summary>
    /// Query historical data for a time range
    /// </summary>
    /// <param name="dataset">Dataset name (e.g., "GLBX.MDP3")</param>
    /// <param name="schema">Schema type</param>
    /// <param name="symbols">List of symbols</param>
    /// <param name="startTime">Start time</param>
    /// <param name="endTime">End time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of records</returns>
    IAsyncEnumerable<Record> GetRangeAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get metadata for a historical query
    /// Note: This feature is currently not fully implemented in the native layer
    /// </summary>
    /// <param name="dataset">Dataset name</param>
    /// <param name="schema">Schema type</param>
    /// <param name="startTime">Start time</param>
    /// <param name="endTime">End time</param>
    /// <returns>Metadata object, or null if not available</returns>
    IMetadata? GetMetadata(
        string dataset,
        Schema schema,
        DateTimeOffset startTime,
        DateTimeOffset endTime);
}
