namespace Databento.Client.Models;

/// <summary>
/// System message - 320 bytes
/// System-level messages including heartbeats
/// </summary>
public class SystemMessage : Record
{
    /// <summary>
    /// System message text
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// System message code (0 = Heartbeat, 1 = SubscriptionAck, etc.)
    /// </summary>
    public byte Code { get; set; }

    /// <summary>
    /// Check if this is a heartbeat message
    /// </summary>
    public bool IsHeartbeat => Code == 0;

    public override string ToString()
    {
        if (IsHeartbeat)
            return $"System: Heartbeat [{Timestamp:O}]";
        return $"System: [{Code}] {Message} [{Timestamp:O}]";
    }
}
