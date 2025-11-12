using Databento.Interop;
using Databento.Interop.Handles;
using Databento.Interop.Native;

namespace Databento.Client.Metadata;

/// <summary>
/// Metadata implementation for querying instrument information.
/// IMPORTANT: This class holds native resources and must be disposed when no longer needed.
/// Use 'using' statements or call Dispose() explicitly to prevent resource leaks.
/// </summary>
/// <remarks>
/// Metadata objects maintain mappings between instrument IDs and symbols.
/// These mappings are backed by native memory that must be freed via disposal.
/// </remarks>
public sealed class Metadata : IMetadata
{
    private readonly MetadataHandle _handle;
    private bool _disposed;

    internal Metadata(MetadataHandle handle)
    {
        _handle = handle ?? throw new ArgumentNullException(nameof(handle));
    }

    /// <summary>
    /// Get symbol string for an instrument ID
    /// </summary>
    public string? GetSymbol(uint instrumentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        byte[] symbolBuffer = new byte[Models.Constants.SymbolCstrLen];

        int result = NativeMethods.dbento_metadata_get_symbol_mapping(
            _handle,
            instrumentId,
            symbolBuffer,
            (nuint)symbolBuffer.Length);

        if (result != 0)
        {
            return null; // Not found or error
        }

        // Convert to string, trimming at null terminator
        string symbol = System.Text.Encoding.UTF8.GetString(symbolBuffer).TrimEnd('\0');
        return string.IsNullOrEmpty(symbol) ? null : symbol;
    }

    /// <summary>
    /// Check if metadata contains mapping for instrument ID
    /// </summary>
    public bool Contains(uint instrumentId)
    {
        return GetSymbol(instrumentId) != null;
    }

    /// <summary>
    /// Create a timeseries symbol map from this metadata.
    /// Useful for working with historical data where symbols may change over time.
    /// </summary>
    public ITsSymbolMap CreateSymbolMap()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // MEDIUM FIX: Increased from 512 to 2048 for full error context
        byte[] errorBuffer = new byte[2048];
        var handlePtr = NativeMethods.dbento_metadata_create_symbol_map(
            _handle,
            errorBuffer,
            (nuint)errorBuffer.Length);

        if (handlePtr == IntPtr.Zero)
        {
            // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
            throw new DbentoException($"Failed to create timeseries symbol map: {error}");
        }

        return new TsSymbolMap(new TsSymbolMapHandle(handlePtr));
    }

    /// <summary>
    /// Create a point-in-time symbol map for a specific date from this metadata.
    /// Useful for working with live data or historical requests over a single day.
    /// </summary>
    public IPitSymbolMap CreateSymbolMapForDate(DateOnly date)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // MEDIUM FIX: Increased from 512 to 2048 for full error context
        byte[] errorBuffer = new byte[2048];
        var handlePtr = NativeMethods.dbento_metadata_create_symbol_map_for_date(
            _handle,
            date.Year,
            (uint)date.Month,
            (uint)date.Day,
            errorBuffer,
            (nuint)errorBuffer.Length);

        if (handlePtr == IntPtr.Zero)
        {
            // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
            throw new DbentoException($"Failed to create point-in-time symbol map: {error}");
        }

        return new PitSymbolMap(new PitSymbolMapHandle(handlePtr));
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _handle?.Dispose();
    }
}
