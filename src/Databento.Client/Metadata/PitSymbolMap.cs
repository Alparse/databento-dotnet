using System.Runtime.InteropServices;
using Databento.Client.Models;
using Databento.Interop;
using Databento.Interop.Handles;
using Databento.Interop.Native;

namespace Databento.Client.Metadata;

/// <summary>
/// Point-in-time symbol map implementation for resolving instrument IDs to symbols
/// </summary>
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
    /// <remarks>
    /// This method is for advanced usage and requires access to raw DBN record bytes.
    /// For most use cases, create symbol maps from Metadata using CreateSymbolMapForDate().
    /// Currently not implemented as Record objects don't preserve raw bytes.
    /// </remarks>
    public void OnRecord(Record record)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // TODO: Implement when we have a way to serialize Record back to raw DBN bytes
        // or modify the streaming pipeline to preserve raw bytes for symbol mapping updates
        throw new NotImplementedException(
            "OnRecord is not yet implemented. Create symbol maps from Metadata instead.");
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _handle?.Dispose();
    }
}
