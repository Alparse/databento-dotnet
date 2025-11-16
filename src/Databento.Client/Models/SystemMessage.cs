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
    /// System message code indicating the type of message
    /// </summary>
    public SystemCode Code { get; set; }

    /// <summary>
    /// Check if this is a heartbeat message
    /// </summary>
    public bool IsHeartbeat => Code == SystemCode.Heartbeat;

    public override string ToString()
    {
        if (IsHeartbeat)
            return $"System: Heartbeat [{Timestamp:O}]";
        return $"System: [{Code}] {Message} [{Timestamp:O}]";
    }
}
