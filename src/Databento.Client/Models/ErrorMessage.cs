namespace Databento.Client.Models;

/// <summary>
/// Error message - 320 bytes
/// Error information from the venue or gateway
/// </summary>
public class ErrorMessage : Record
{
    /// <summary>
    /// Error message text
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Error code
    /// </summary>
    public byte Code { get; set; }

    /// <summary>
    /// Whether this is the last error message in a sequence
    /// </summary>
    public bool IsLast { get; set; }

    public override string ToString()
    {
        return $"Error: [{Code}] {Error} (Last: {IsLast})";
    }
}
