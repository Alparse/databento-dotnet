using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Databento.Client.Models;
using Databento.Client.Models.Dbn;
using Databento.Interop;
using Databento.Interop.Handles;
using Databento.Interop.Native;

namespace Databento.Client.Dbn;

/// <summary>
/// Reader for DBN (Databento Binary) format files
/// </summary>
public sealed class DbnFileReader : IDbnFileReader
{
    private readonly DbnFileReaderHandle _handle;
    private DbnMetadata? _cachedMetadata;
    private bool _disposed;

    /// <summary>
    /// Open a DBN file for reading
    /// </summary>
    /// <param name="filePath">Path to the DBN file</param>
    /// <exception cref="FileNotFoundException">If the file does not exist</exception>
    /// <exception cref="DbentoException">If the file cannot be opened or is invalid</exception>
    public DbnFileReader(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"DBN file not found: {filePath}", filePath);

        // MEDIUM FIX: Increased from 512 to 2048 for full error context
        byte[] errorBuffer = new byte[2048];
        var handlePtr = NativeMethods.dbento_dbn_file_open(
            filePath,
            errorBuffer,
            (nuint)errorBuffer.Length);

        if (handlePtr == IntPtr.Zero)
        {
            // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
            throw new DbentoException($"Failed to open DBN file: {error}");
        }

        _handle = new DbnFileReaderHandle(handlePtr);
    }

    /// <summary>
    /// Get metadata about the DBN file
    /// </summary>
    /// <returns>DBN file metadata</returns>
    public DbnMetadata GetMetadata()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Return cached metadata if available
        if (_cachedMetadata != null)
            return _cachedMetadata;

        // MEDIUM FIX: Increased from 512 to 2048 for full error context
        byte[] errorBuffer = new byte[2048];
        var jsonPtr = NativeMethods.dbento_dbn_file_get_metadata(
            _handle,
            errorBuffer,
            (nuint)errorBuffer.Length);

        if (jsonPtr == IntPtr.Zero)
        {
            // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
            throw new DbentoException($"Failed to get DBN file metadata: {error}");
        }

        try
        {
            var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "{}";
            _cachedMetadata = JsonSerializer.Deserialize<DbnMetadata>(json)
                ?? throw new DbentoException("Failed to deserialize DBN file metadata");
            return _cachedMetadata;
        }
        finally
        {
            NativeMethods.dbento_free_string(jsonPtr);
        }
    }

    /// <summary>
    /// Read all records from the DBN file as an async stream
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of records</returns>
    public async IAsyncEnumerable<Record> ReadRecordsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Use a reasonably large buffer for records
        byte[] recordBuffer = new byte[8192];
        // MEDIUM FIX: Increased from 512 to 2048 for full error context
        byte[] errorBuffer = new byte[2048];

        await Task.Yield(); // Make it properly async

        // MEDIUM FIX: Track record number for better error messages
        ulong recordNumber = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            int result = NativeMethods.dbento_dbn_file_next_record(
                _handle,
                recordBuffer,
                (nuint)recordBuffer.Length,
                out nuint recordLength,
                out byte recordType,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (result == 1)
            {
                // EOF reached
                yield break;
            }

            if (result < 0)
            {
                // Error occurred
                // HIGH FIX: Use safe error string extraction
                // MEDIUM FIX: Include record number in error message
                var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                throw new DbentoException($"Error reading DBN file record #{recordNumber}: {error}");
            }

            // Success - parse the record
            if (recordLength > 0)
            {
                byte[] recordBytes = new byte[recordLength];
                Array.Copy(recordBuffer, recordBytes, (int)recordLength);

                // MEDIUM FIX: Wrap deserialization to provide better error context
                Record record;
                try
                {
                    record = Record.FromBytes(recordBytes, recordType);
                }
                catch (Exception ex)
                {
                    // Include record number in error for better debugging
                    throw new DbentoException($"Error deserializing DBN file record #{recordNumber}: {ex.Message}", ex);
                }

                recordNumber++;
                yield return record;
            }

            // Allow cancellation between records
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// Dispose the file reader and free resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _handle?.Dispose();
    }
}
