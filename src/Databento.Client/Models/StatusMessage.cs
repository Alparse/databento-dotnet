namespace Databento.Client.Models;

/// <summary>
/// Trading status message - 40 bytes
/// Indicates trading state changes for an instrument
/// </summary>
public class StatusMessage : Record
{
    /// <summary>
    /// Timestamp when received by Databento (nanoseconds)
    /// </summary>
    public long TsRecv { get; set; }

    /// <summary>
    /// Status action (PreOpen, Trading, Halt, etc.)
    /// </summary>
    public StatusAction Action { get; set; }

    /// <summary>
    /// Reason for status change
    /// </summary>
    public StatusReason Reason { get; set; }

    /// <summary>
    /// Trading event type
    /// </summary>
    public TradingEvent TradingEvent { get; set; }

    /// <summary>
    /// Whether trading is allowed
    /// </summary>
    public TriState IsTrading { get; set; }

    /// <summary>
    /// Whether quoting is allowed
    /// </summary>
    public TriState IsQuoting { get; set; }

    /// <summary>
    /// Whether short selling is restricted
    /// </summary>
    public TriState IsShortSellRestricted { get; set; }

    /// <summary>
    /// Get receive timestamp as DateTimeOffset
    /// </summary>
    public DateTimeOffset TsRecvTime =>
        DateTimeOffset.FromUnixTimeMilliseconds(TsRecv / 1_000_000);

    public override string ToString()
    {
        return $"Status: {Action} - {Reason} | Trading:{IsTrading} Quoting:{IsQuoting} [{Timestamp:O}]";
    }
}
