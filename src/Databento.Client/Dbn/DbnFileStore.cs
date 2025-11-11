using Databento.Client.Models;
using Databento.Client.Models.Dbn;

namespace Databento.Client.Dbn;

/// <summary>
/// Enhanced DBN file reader with both callback and blocking APIs
/// Provides flexible access to DBN file data through multiple patterns
/// </summary>
public sealed class DbnFileStore : IDbnFileStore
{
    private readonly string _filePath;
    private DbnMetadata? _metadata;
    private DbnFileReader? _blockingReader;
    private IAsyncEnumerator<Record>? _recordEnumerator;
    private bool _disposed;

    /// <summary>
    /// Create a new DBN file store
    /// </summary>
    /// <param name="filePath">Path to the DBN file</param>
    public DbnFileStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        _filePath = filePath;
    }

    /// <summary>
    /// Get the metadata from the DBN file (decoded lazily on first access)
    /// </summary>
    public DbnMetadata Metadata
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_metadata == null)
            {
                // Read metadata using DbnFileReader
                using var reader = new DbnFileReader(_filePath);
                _metadata = reader.GetMetadata();
            }

            return _metadata;
        }
    }

    /// <summary>
    /// Replay all records in the file using callbacks
    /// </summary>
    /// <param name="metadataCallback">Callback for metadata (called once before records)</param>
    /// <param name="recordCallback">Callback for each record (return false to stop)</param>
    public void Replay(MetadataCallback? metadataCallback, RecordCallback recordCallback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(recordCallback);

        // Call metadata callback if provided
        metadataCallback?.Invoke(Metadata);

        // Create a new reader for this replay
        using var reader = new DbnFileReader(_filePath);

        // Process all records
        var enumerator = reader.ReadRecordsAsync().GetAsyncEnumerator();
        try
        {
            while (true)
            {
                // Synchronously wait for next record
                var task = enumerator.MoveNextAsync();
                if (!task.AsTask().GetAwaiter().GetResult())
                    break;

                var record = enumerator.Current;

                // Call callback and check if we should continue
                if (!recordCallback(record))
                    break;
            }
        }
        finally
        {
            enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// Replay all records in the file using a record callback only
    /// </summary>
    /// <param name="recordCallback">Callback for each record (return false to stop)</param>
    public void Replay(RecordCallback recordCallback)
    {
        Replay(null, recordCallback);
    }

    /// <summary>
    /// Read the next record from the file (blocking iteration)
    /// </summary>
    /// <returns>The next record, or null if no more records</returns>
    public Record? NextRecord()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Initialize reader and enumerator on first call
        if (_recordEnumerator == null)
        {
            _blockingReader = new DbnFileReader(_filePath);
            _recordEnumerator = _blockingReader.ReadRecordsAsync().GetAsyncEnumerator();
        }

        // Try to move to next record (synchronously)
        var moveTask = _recordEnumerator.MoveNextAsync();
        bool hasNext = moveTask.AsTask().GetAwaiter().GetResult();

        if (hasNext)
        {
            return _recordEnumerator.Current;
        }

        // No more records
        return null;
    }

    /// <summary>
    /// Reset the reader to the beginning of the file
    /// </summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Dispose existing enumerator and reader
        if (_recordEnumerator != null)
        {
            _recordEnumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _recordEnumerator = null;
        }

        _blockingReader?.Dispose();
        _blockingReader = null;
    }

    /// <summary>
    /// Dispose the file store and release resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        if (_recordEnumerator != null)
        {
            _recordEnumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _recordEnumerator = null;
        }

        _blockingReader?.Dispose();
        _blockingReader = null;
    }
}
