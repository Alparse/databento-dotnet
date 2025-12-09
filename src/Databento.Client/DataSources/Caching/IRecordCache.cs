using Databento.Client.Models;

namespace Databento.Client.DataSources.Caching;

/// <summary>
/// Abstraction for caching historical records.
/// Enables repeated replay without re-fetching from API.
/// </summary>
public interface IRecordCache : IAsyncDisposable
{
    /// <summary>
    /// Unique key identifying this cache (based on query parameters).
    /// </summary>
    string CacheKey { get; }

    /// <summary>
    /// Total number of records in cache (if known).
    /// </summary>
    long? RecordCount { get; }

    /// <summary>
    /// Whether the cache has data for the given query.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Write records to the cache.
    /// </summary>
    /// <param name="records">Records to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task WriteAsync(IAsyncEnumerable<Record> records, CancellationToken cancellationToken = default);

    /// <summary>
    /// Read all records from cache.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    IAsyncEnumerable<Record> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Read records starting from a specific index (for resume).
    /// </summary>
    /// <param name="startIndex">Index to start reading from (0-based)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    IAsyncEnumerable<Record> ReadFromIndexAsync(long startIndex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate/delete the cache.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InvalidateAsync(CancellationToken cancellationToken = default);
}
