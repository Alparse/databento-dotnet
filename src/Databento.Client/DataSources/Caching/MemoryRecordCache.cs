using System.Runtime.CompilerServices;
using Databento.Client.Models;

namespace Databento.Client.DataSources.Caching;

/// <summary>
/// Caches records in memory for fast replay.
/// Best for smaller datasets or when you need fast random access.
/// </summary>
public sealed class MemoryRecordCache : IRecordCache
{
    private List<Record>? _records;
    private readonly object _lock = new();

    /// <inheritdoc/>
    public string CacheKey { get; }

    /// <inheritdoc/>
    public long? RecordCount
    {
        get
        {
            lock (_lock)
            {
                return _records?.Count;
            }
        }
    }

    /// <summary>
    /// Creates a new memory cache with the specified key.
    /// </summary>
    /// <param name="cacheKey">Unique key for this cache</param>
    public MemoryRecordCache(string cacheKey)
    {
        CacheKey = cacheKey ?? throw new ArgumentNullException(nameof(cacheKey));
    }

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_records != null && _records.Count > 0);
        }
    }

    /// <inheritdoc/>
    public async Task WriteAsync(IAsyncEnumerable<Record> records, CancellationToken cancellationToken = default)
    {
        var newList = new List<Record>();

        await foreach (var record in records.WithCancellation(cancellationToken))
        {
            newList.Add(record);
        }

        lock (_lock)
        {
            _records = newList;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Record> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<Record>? snapshot;
        lock (_lock)
        {
            snapshot = _records;
        }

        if (snapshot == null)
            yield break;

        foreach (var record in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return record;
        }

        await Task.CompletedTask; // Keep async signature
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Record> ReadFromIndexAsync(long startIndex, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<Record>? snapshot;
        lock (_lock)
        {
            snapshot = _records;
        }

        if (snapshot == null)
            yield break;

        for (var i = (int)Math.Max(0, startIndex); i < snapshot.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return snapshot[i];
        }

        await Task.CompletedTask; // Keep async signature
    }

    /// <inheritdoc/>
    public Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _records = null;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        lock (_lock)
        {
            _records = null;
        }
        return ValueTask.CompletedTask;
    }
}
