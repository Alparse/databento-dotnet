using System.Runtime.InteropServices;
using Databento.Client.Models;
using Databento.Interop;
using Databento.Interop.Handles;
using Databento.Interop.Native;

namespace Databento.Client.Metadata;

/// <summary>
/// Point-in-time symbol map implementation for resolving instrument IDs to symbols.
/// IMPORTANT: This class holds native resources and must be disposed when no longer needed.
/// Use 'using' statements or call Dispose() explicitly to prevent resource leaks.
/// </summary>
/// <remarks>
/// Point-in-time symbol maps maintain mappings for a specific point in time (e.g., for a single trading day).
/// These mappings are backed by native memory that must be freed via disposal.
/// </remarks>
public sealed class PitSymbolMap : IPitSymbolMap
{
    private readonly PitSymbolMapHandle _handle;
    private bool _disposed;

    internal PitSymbolMap(PitSymbolMapHandle handle)
    {
        _handle = handle ?? throw new ArgumentNullException(nameof(handle));
    }

    /// <summary>
    /// Whether the symbol map is empty
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            int result = NativeMethods.dbento_pit_symbol_map_is_empty(_handle);
            return result == 1;
        }
    }

    /// <summary>
    /// Number of mappings in the symbol map
    /// </summary>
    public int Size
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            nuint size = NativeMethods.dbento_pit_symbol_map_size(_handle);
            return (int)size;
        }
    }

    /// <summary>
    /// Find symbol for an instrument ID
    /// </summary>
    public string? Find(uint instrumentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        byte[] symbolBuffer = new byte[Models.Constants.SymbolCstrLen];

        int result = NativeMethods.dbento_pit_symbol_map_find(
            _handle,
            instrumentId,
            symbolBuffer,
            (nuint)symbolBuffer.Length);

        if (result != 0)
        {
            return null; // Not found or error
        }

        string symbol = System.Text.Encoding.UTF8.GetString(symbolBuffer).TrimEnd('\0');
        return string.IsNullOrEmpty(symbol) ? null : symbol;
    }

    /// <summary>
    /// Get symbol for an instrument ID (throws if not found)
    /// </summary>
    public string At(uint instrumentId)
    {
        string? symbol = Find(instrumentId);
        if (symbol == null)
        {
            throw new KeyNotFoundException(
                $"No symbol found for instrument ID {instrumentId}");
        }
        return symbol;
    }

    /// <summary>
    /// Update symbol map from a record (for live data)
    /// </summary>
    /// <param name="record">Record to process (only SymbolMapping records update the map)</param>
    /// <remarks>
    /// This method updates the internal symbol map when SymbolMapping records are received.
    /// It is primarily used during live streaming to dynamically build symbol mappings.
    /// Only records of type SymbolMapping (RType 0x1B) will update the map; all other record types are silently ignored.
    /// For historical data, prefer using Metadata.CreateSymbolMap() or CreateSymbolMapForDate() instead.
    /// </remarks>
    /// <exception cref="InvalidOperationException">If the record does not have raw bytes available</exception>
    /// <exception cref="DbentoException">If the native operation fails</exception>
    public void OnRecord(Record record)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(record);

        if (record.RawBytes == null || record.RawBytes.Length == 0)
        {
            throw new InvalidOperationException(
                "Record does not have raw bytes available. " +
                "Only records read from DBN streams can be used with OnRecord().");
        }

        int result = NativeMethods.dbento_pit_symbol_map_on_record(
            _handle,
            record.RawBytes,
            (nuint)record.RawBytes.Length);

        if (result != 0)
        {
            throw new DbentoException(
                "Failed to update PIT symbol map from record. " +
                $"Instrument ID: {record.InstrumentId}, RType: 0x{record.RType:X2}");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _handle?.Dispose();
    }
}
