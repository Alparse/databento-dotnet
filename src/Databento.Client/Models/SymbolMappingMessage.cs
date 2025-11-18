namespace Databento.Client.Models;

/// <summary>
/// Symbol mapping message that maps InstrumentId to ticker symbols.
/// These records are sent at the START of a data stream, before trade/quote records.
/// You must handle these records to resolve symbols in subsequent data records.
/// </summary>
/// <remarks>
/// <para><strong>Example Workflow:</strong></para>
/// <para>For NVDA trades:</para>
/// <list type="number">
///   <item>SymbolMappingMessage arrives first: InstrumentId=11667, STypeOutSymbol="NVDA"</item>
///   <item>Store mapping: symbolMap[11667] = "NVDA"</item>
///   <item>TradeMessage arrives: InstrumentId=11667</item>
///   <item>Resolve symbol: symbol = symbolMap[11667] returns "NVDA"</item>
/// </list>
/// <para><strong>⚠️ IMPORTANT: Use STypeOutSymbol, not STypeInSymbol!</strong></para>
/// <para>When subscribing to multiple symbols or ALL_SYMBOLS:</para>
/// <list type="bullet">
///   <item>STypeInSymbol = "ALL_SYMBOLS" (your subscription string - same for all records)</item>
///   <item>STypeOutSymbol = "NVDA", "AAPL", "TSLA", etc. (the actual ticker symbols)</item>
/// </list>
/// <para><strong>Common Usage Pattern:</strong></para>
/// <code>
/// var symbolMap = new ConcurrentDictionary&lt;uint, string&gt;();
///
/// client.DataReceived += (sender, e) => {
///     if (e.Record is SymbolMappingMessage mapping) {
///         // ⚠️ Use STypeOutSymbol for the actual ticker symbol!
///         symbolMap[mapping.InstrumentId] = mapping.STypeOutSymbol;
///     }
///     else if (e.Record is TradeMessage trade) {
///         var symbol = symbolMap.GetValueOrDefault(
///             trade.InstrumentId,
///             trade.InstrumentId.ToString());
///         Console.WriteLine($"{symbol}: ${trade.PriceDecimal}");
///     }
/// };
/// </code>
/// </remarks>
public class SymbolMappingMessage : Record
{
    /// <summary>
    /// Input symbol type from your subscription.
    /// </summary>
    public SType STypeIn { get; set; }

    /// <summary>
    /// The subscription symbol string (what you passed to SubscribeAsync).
    /// ⚠️ For multi-symbol subscriptions (e.g., "ALL_SYMBOLS"), this will be
    /// the same for ALL records. Use <see cref="STypeOutSymbol"/> instead to get individual ticker symbols.
    /// </summary>
    /// <example>
    /// Single symbol: Subscribe("NVDA") → STypeInSymbol = "NVDA"
    /// Multi-symbol:  Subscribe("ALL_SYMBOLS") → STypeInSymbol = "ALL_SYMBOLS" (for all 12,000+ records)
    /// </example>
    public string STypeInSymbol { get; set; } = string.Empty;

    /// <summary>
    /// Output symbol type (resolved symbol format).
    /// </summary>
    public SType STypeOut { get; set; }

    /// <summary>
    /// The resolved ticker symbol for this specific instrument.
    /// ✅ ALWAYS use this property to display ticker symbols to users.
    /// </summary>
    /// <example>
    /// Single symbol: Subscribe("NVDA") → STypeOutSymbol = "NVDA"
    /// Multi-symbol:  Subscribe("ALL_SYMBOLS") → STypeOutSymbol = "NVDA", "AAPL", "TSLA", etc.
    /// </example>
    /// <remarks>
    /// This property contains the actual ticker symbol regardless of subscription type.
    /// Using <see cref="STypeInSymbol"/> for ALL_SYMBOLS subscriptions will result in
    /// all trades showing "ALL_SYMBOLS" instead of individual ticker symbols.
    /// </remarks>
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
