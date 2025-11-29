using System;

namespace Databento.Client.Resilience;

/// <summary>
/// Configuration for connection resilience features.
/// </summary>
public sealed class ResilienceOptions
{
    /// <summary>
    /// Whether to automatically reconnect on connection failure.
    /// When enabled, transient errors trigger automatic reconnection
    /// using the configured RetryPolicy. Default is false.
    /// </summary>
    public bool AutoReconnect { get; set; } = false;

    /// <summary>
    /// Whether to automatically resubscribe after reconnection.
    /// Only applies when AutoReconnect is true. Default is true.
    /// </summary>
    public bool AutoResubscribe { get; set; } = true;

    /// <summary>
    /// Retry policy for connection attempts.
    /// </summary>
    public RetryPolicy RetryPolicy { get; set; } = RetryPolicy.Default;

    /// <summary>
    /// Timeout for heartbeat responses. If no heartbeat is received
    /// within this duration, the connection is considered stale.
    /// Set to TimeSpan.Zero to disable heartbeat monitoring.
    /// Default is 90 seconds (3x the default 30s heartbeat interval).
    /// </summary>
    public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Callback invoked before each reconnection attempt.
    /// Return false to cancel the reconnection.
    /// </summary>
    public Func<int, Exception, bool>? OnReconnecting { get; set; }

    /// <summary>
    /// Callback invoked after successful reconnection.
    /// </summary>
    public Action<int>? OnReconnected { get; set; }

    /// <summary>
    /// Callback invoked when all retry attempts are exhausted.
    /// </summary>
    public Action<Exception>? OnReconnectFailed { get; set; }

    /// <summary>
    /// Default options with no auto-reconnect.
    /// </summary>
    public static ResilienceOptions Default => new();

    /// <summary>
    /// Options with auto-reconnect enabled using default retry policy.
    /// </summary>
    public static ResilienceOptions WithAutoReconnect => new()
    {
        AutoReconnect = true,
        AutoResubscribe = true,
        RetryPolicy = RetryPolicy.Default
    };

    /// <summary>
    /// Options for high-availability scenarios.
    /// Aggressive retries, auto-reconnect, short heartbeat timeout.
    /// </summary>
    public static ResilienceOptions HighAvailability => new()
    {
        AutoReconnect = true,
        AutoResubscribe = true,
        RetryPolicy = RetryPolicy.Aggressive,
        HeartbeatTimeout = TimeSpan.FromSeconds(60)
    };
}
