namespace Databento.Client.Live;

/// <summary>
/// Action to take when an exception occurs during streaming (matches databento-cpp ExceptionAction)
/// </summary>
public enum ExceptionAction
{
    /// <summary>
    /// Continue processing after the exception
    /// Matches C++ ExceptionAction::Continue
    /// </summary>
    Continue,

    /// <summary>
    /// Stop streaming and clean up
    /// Matches C++ ExceptionAction::Stop
    /// </summary>
    Stop
}

/// <summary>
/// Callback delegate for handling exceptions during streaming (matches databento-cpp ExceptionCallback)
/// </summary>
/// <param name="exception">The exception that occurred</param>
/// <returns>Action to take (Continue or Stop)</returns>
/// <remarks>
/// This callback is invoked when an exception occurs during data processing.
/// Return ExceptionAction.Continue to continue streaming, or ExceptionAction.Stop to terminate.
/// Matches C++ API: using ExceptionCallback = std::function&lt;ExceptionAction(const std::exception&amp;)&gt;;
/// </remarks>
public delegate ExceptionAction ExceptionCallback(Exception exception);
