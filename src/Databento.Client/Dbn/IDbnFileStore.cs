namespace Databento.Client.Dbn;

/// <summary>
/// Delegate for processing metadata from a DBN file
/// </summary>
/// <param name="metadata">The metadata from the DBN file</param>
public delegate void MetadataCallback(Models.Dbn.DbnMetadata metadata);

/// <summary>
/// Delegate for processing records from a DBN file
/// </summary>
/// <param name="record">The record to process</param>
/// <returns>True to continue processing, false to stop</returns>
public delegate bool RecordCallback(Models.Record record);

/// <summary>
/// Enhanced DBN file reader with both callback and blocking APIs
/// </summary>
public interface IDbnFileStore : IDisposable
{
    /// <summary>
    /// Get the metadata from the DBN file (decoded lazily on first access)
    /// </summary>
    Models.Dbn.DbnMetadata Metadata { get; }

    /// <summary>
    /// Replay all records in the file using callbacks
    /// </summary>
    /// <param name="metadataCallback">Callback for metadata (called once before records)</param>
    /// <param name="recordCallback">Callback for each record</param>
    void Replay(MetadataCallback? metadataCallback, RecordCallback recordCallback);

    /// <summary>
    /// Replay all records in the file using a record callback only
    /// </summary>
    /// <param name="recordCallback">Callback for each record</param>
    void Replay(RecordCallback recordCallback);

    /// <summary>
    /// Read the next record from the file (blocking iteration)
    /// </summary>
    /// <returns>The next record, or null if no more records</returns>
    Models.Record? NextRecord();

    /// <summary>
    /// Reset the reader to the beginning of the file
    /// </summary>
    void Reset();
}
