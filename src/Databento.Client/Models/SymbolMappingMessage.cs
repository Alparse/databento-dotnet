namespace Databento.Client.Models;

/// <summary>
/// Symbol mapping message - 176 bytes
/// Maps between different symbol types for an instrument
/// </summary>
public class SymbolMappingMessage : Record
{
    /// <summary>
    /// Input symbol type
    /// </summary>
    public SType STypeIn { get; set; }

    /// <summary>
    /// Input symbol string
    /// </summary>
    public string STypeInSymbol { get; set; } = string.Empty;

    /// <summary>
    /// Output symbol type
    /// </summary>
    public SType STypeOut { get; set; }

    /// <summary>
    /// Output symbol string
    /// </summary>
    public string STypeOutSymbol { get; set; } = string.Empty;

    /// <summary>
    /// Start timestamp for this mapping (nanoseconds)
    /// </summary>
    public long StartTs { get; set; }

    /// <summary>
    /// End timestamp for this mapping (nanoseconds)
    /// </summary>
    public long EndTs { get; set; }

    public DateTimeOffset StartTime =>
        DateTimeOffset.FromUnixTimeMilliseconds(StartTs / 1_000_000);

    public DateTimeOffset EndTime =>
        DateTimeOffset.FromUnixTimeMilliseconds(EndTs / 1_000_000);

    public override string ToString()
    {
        return $"SymbolMapping: {STypeInSymbol} ({STypeIn}) -> {STypeOutSymbol} ({STypeOut}) [{StartTime:O} to {EndTime:O}]";
    }
}
