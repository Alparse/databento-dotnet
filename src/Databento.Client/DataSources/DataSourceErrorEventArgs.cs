namespace Databento.Client.DataSources;

/// <summary>
/// Event arguments for data source errors.
/// </summary>
public sealed class DataSourceErrorEventArgs : EventArgs
{
    /// <summary>
    /// The exception that occurred.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Hint for whether the error is recoverable (e.g., temporary network issue).
    /// </summary>
    public bool IsRecoverable { get; }

    /// <summary>
    /// Optional error code from the underlying source.
    /// </summary>
    public int? ErrorCode { get; }

    /// <summary>
    /// Creates a new instance of DataSourceErrorEventArgs.
    /// </summary>
    /// <param name="exception">The exception that occurred</param>
    /// <param name="isRecoverable">Whether the error is recoverable</param>
    /// <param name="errorCode">Optional error code</param>
    public DataSourceErrorEventArgs(Exception exception, bool isRecoverable = false, int? errorCode = null)
    {
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        IsRecoverable = isRecoverable;
        ErrorCode = errorCode;
    }
}
