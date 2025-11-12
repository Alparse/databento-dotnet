using System.Runtime.InteropServices;
using System.Text.Json;
using Databento.Client.Models;
using Databento.Client.Models.Dbn;
using Databento.Interop;
using Databento.Interop.Handles;
using Databento.Interop.Native;

namespace Databento.Client.Dbn;

/// <summary>
/// Writer for DBN (Databento Binary) format files
/// </summary>
public sealed class DbnFileWriter : IDbnFileWriter
{
    private readonly DbnFileWriterHandle _handle;
    private readonly string _filePath;
    private bool _disposed;

    /// <summary>
    /// Create a new DBN file writer
    /// </summary>
    /// <param name="filePath">Path where the DBN file will be created</param>
    /// <param name="metadata">Metadata for the DBN file</param>
    /// <exception cref="ArgumentException">If file path or metadata is invalid</exception>
    /// <exception cref="DbentoException">If the file cannot be created</exception>
    public DbnFileWriter(string filePath, DbnMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        ArgumentNullException.ThrowIfNull(metadata);

        _filePath = filePath;

        // Serialize metadata to JSON
        string metadataJson = JsonSerializer.Serialize(metadata);

        // MEDIUM FIX: Increased from 512 to 2048 for full error context
        byte[] errorBuffer = new byte[2048];
        var handlePtr = NativeMethods.dbento_dbn_file_create(
            filePath,
            metadataJson,
            errorBuffer,
            (nuint)errorBuffer.Length);

        if (handlePtr == IntPtr.Zero)
        {
            // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
            throw new DbentoException($"Failed to create DBN file: {error}");
        }

        _handle = new DbnFileWriterHandle(handlePtr);
    }

    /// <summary>
    /// Write a single record to the DBN file
    /// </summary>
    /// <param name="record">Record to write</param>
    public void WriteRecord(Record record)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(record);

        if (record.RawBytes == null || record.RawBytes.Length == 0)
            throw new InvalidOperationException("Record does not have raw bytes available for writing. " +
                "Only records read from DBN files can be written.");

        // MEDIUM FIX: Increased from 512 to 2048 for full error context
        byte[] errorBuffer = new byte[2048];
        int result = NativeMethods.dbento_dbn_file_write_record(
            _handle,
            record.RawBytes,
            (nuint)record.RawBytes.Length,
            errorBuffer,
            (nuint)errorBuffer.Length);

        if (result != 0)
        {
            // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
            throw new DbentoException($"Failed to write record to DBN file: {error}");
        }
    }

    /// <summary>
    /// Write multiple records to the DBN file
    /// </summary>
    /// <param name="records">Records to write</param>
    public void WriteRecords(IEnumerable<Record> records)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(records);

        foreach (var record in records)
        {
            WriteRecord(record);
        }
    }

    /// <summary>
    /// Flush any buffered data to disk
    /// </summary>
    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // The native layer automatically flushes on write
        // This method is provided for API consistency
    }

    /// <summary>
    /// Dispose the file writer and finalize the file
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _handle?.Dispose();
    }

    /// <summary>
    /// MEDIUM FIX: Asynchronously dispose the file writer and finalize the file
    /// Provides proper async cleanup for async usage patterns
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
