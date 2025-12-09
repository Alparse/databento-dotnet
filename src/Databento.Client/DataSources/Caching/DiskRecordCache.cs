using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Databento.Client.Dbn;
using Databento.Client.Models;
using Databento.Client.Models.Dbn;

namespace Databento.Client.DataSources.Caching;

/// <summary>
/// Caches historical data to DBN files on disk.
/// Persists across process restarts for unlimited replay.
/// </summary>
public sealed class DiskRecordCache : IRecordCache
{
    private readonly string _cacheDirectory;
    private readonly string _cacheFilePath;
    private readonly DbnMetadata _metadata;
    private long? _recordCount;

    /// <inheritdoc/>
    public string CacheKey { get; }

    /// <inheritdoc/>
    public long? RecordCount => _recordCount;

    /// <summary>
    /// Creates a new disk cache.
    /// </summary>
    /// <param name="cacheDirectory">Directory for cache files</param>
    /// <param name="cacheKey">Unique key for this cache</param>
    /// <param name="metadata">DBN metadata for the cache file</param>
    public DiskRecordCache(string cacheDirectory, string cacheKey, DbnMetadata metadata)
    {
        _cacheDirectory = cacheDirectory ?? throw new ArgumentNullException(nameof(cacheDirectory));
        CacheKey = cacheKey ?? throw new ArgumentNullException(nameof(cacheKey));
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _cacheFilePath = Path.Combine(cacheDirectory, $"{cacheKey}.dbn");
    }

    /// <summary>
    /// Generate a deterministic cache key from query parameters.
    /// </summary>
    public static string GenerateCacheKey(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        var symbolsHash = string.Join(",", symbols.OrderBy(s => s, StringComparer.Ordinal));
        var raw = $"{dataset}|{schema}|{symbolsHash}|{start:O}|{end:O}";

        // Create stable hash for filename
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    /// <summary>
    /// Get the default cache directory for the current platform.
    /// </summary>
    public static string GetDefaultCacheDirectory()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(baseDir, "Databento", "Cache");
    }

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(File.Exists(_cacheFilePath));
    }

    /// <inheritdoc/>
    public async Task WriteAsync(IAsyncEnumerable<Record> records, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_cacheDirectory);

        var tempPath = _cacheFilePath + ".tmp";
        long count = 0;

        try
        {
            using var writer = new DbnFileWriter(tempPath, _metadata);

            await foreach (var record in records.WithCancellation(cancellationToken))
            {
                // Only write records that have raw bytes
                if (record.RawBytes != null && record.RawBytes.Length > 0)
                {
                    writer.WriteRecord(record);
                    count++;
                }
            }

            writer.Flush();
        }
        catch
        {
            // Clean up temp file on failure
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* ignore */ }
            }
            throw;
        }

        // Atomic rename
        if (File.Exists(_cacheFilePath))
        {
            File.Delete(_cacheFilePath);
        }
        File.Move(tempPath, _cacheFilePath);
        _recordCount = count;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Record> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_cacheFilePath))
            yield break;

        using var reader = new DbnFileReader(_cacheFilePath);
        long index = 0;

        await foreach (var record in reader.ReadRecordsAsync(cancellationToken))
        {
            index++;
            yield return record;
        }

        _recordCount = index;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Record> ReadFromIndexAsync(long startIndex, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_cacheFilePath))
            yield break;

        using var reader = new DbnFileReader(_cacheFilePath);
        long index = 0;

        await foreach (var record in reader.ReadRecordsAsync(cancellationToken))
        {
            if (index >= startIndex)
            {
                yield return record;
            }
            index++;
        }

        _recordCount = index;
    }

    /// <inheritdoc/>
    public Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_cacheFilePath))
        {
            File.Delete(_cacheFilePath);
        }
        _recordCount = null;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        // Nothing to dispose - file handles are opened/closed per operation
        return ValueTask.CompletedTask;
    }
}
