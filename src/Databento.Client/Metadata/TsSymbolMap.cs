using Databento.Interop;
using Databento.Interop.Handles;
using Databento.Interop.Native;

namespace Databento.Client.Metadata;

/// <summary>
/// Timeseries symbol map implementation for resolving instrument IDs to symbols across time
/// </summary>
public sealed class TsSymbolMap : ITsSymbolMap
{
    private readonly TsSymbolMapHandle _handle;
    private bool _disposed;

    internal TsSymbolMap(TsSymbolMapHandle handle)
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
            int result = NativeMethods.dbento_ts_symbol_map_is_empty(_handle);
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
            nuint size = NativeMethods.dbento_ts_symbol_map_size(_handle);
            return (int)size;
        }
    }

    /// <summary>
    /// Find symbol for an instrument ID on a specific date
    /// </summary>
    public string? Find(DateOnly date, uint instrumentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        byte[] symbolBuffer = new byte[Models.Constants.SymbolCstrLen];

        int result = NativeMethods.dbento_ts_symbol_map_find(
            _handle,
            date.Year,
            (uint)date.Month,
            (uint)date.Day,
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
    /// Get symbol for an instrument ID on a specific date (throws if not found)
    /// </summary>
    public string At(DateOnly date, uint instrumentId)
    {
        string? symbol = Find(date, instrumentId);
        if (symbol == null)
        {
            throw new KeyNotFoundException(
                $"No symbol found for instrument ID {instrumentId} on date {date}");
        }
        return symbol;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _handle?.Dispose();
    }
}
