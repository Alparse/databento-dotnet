using Databento.Interop;
using Databento.Interop.Handles;
using Databento.Interop.Native;

namespace Databento.Client.Metadata;

/// <summary>
/// Metadata implementation for querying instrument information
/// </summary>
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

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _handle?.Dispose();
    }
}
