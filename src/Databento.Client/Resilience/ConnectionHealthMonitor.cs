using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Databento.Client.Resilience;

/// <summary>
/// Monitors connection health and triggers reconnection when needed.
/// </summary>
internal sealed class ConnectionHealthMonitor : IDisposable
{
    private readonly ResilienceOptions _options;
    private readonly ILogger _logger;
    private readonly Func<CancellationToken, Task> _reconnectAction;
    private readonly CancellationTokenSource _cts = new();

    private DateTime _lastActivityTime = DateTime.UtcNow;
    private Task? _monitorTask;
    private int _isReconnecting = 0;
    private int _consecutiveFailures = 0;

    /// <summary>
    /// Event raised when connection health changes.
    /// </summary>
    public event EventHandler<ConnectionHealthEventArgs>? HealthChanged;

    /// <summary>
    /// Current connection health status.
    /// </summary>
    public ConnectionHealth CurrentHealth { get; private set; } = ConnectionHealth.Unknown;

    /// <summary>
    /// Number of successful reconnections since start.
    /// </summary>
    public int ReconnectionCount { get; private set; } = 0;

    /// <summary>
    /// Time of last successful activity (data received or heartbeat).
    /// </summary>
    public DateTime LastActivityTime => _lastActivityTime;

    public ConnectionHealthMonitor(
        ResilienceOptions options,
        ILogger logger,
        Func<CancellationToken, Task> reconnectAction)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _reconnectAction = reconnectAction ?? throw new ArgumentNullException(nameof(reconnectAction));
    }

    /// <summary>
    /// Start the health monitor.
    /// </summary>
    public void Start()
    {
        if (_monitorTask != null)
            return;

        _lastActivityTime = DateTime.UtcNow;
        UpdateHealth(ConnectionHealth.Healthy);

        if (_options.HeartbeatTimeout > TimeSpan.Zero)
        {
            _monitorTask = Task.Run(MonitorLoopAsync);
        }
    }

    /// <summary>
    /// Record activity (data received, heartbeat, etc.).
    /// Call this whenever data flows through the connection.
    /// </summary>
    public void RecordActivity()
    {
        _lastActivityTime = DateTime.UtcNow;
        _consecutiveFailures = 0;

        if (CurrentHealth != ConnectionHealth.Healthy)
        {
            UpdateHealth(ConnectionHealth.Healthy);
        }
    }

    /// <summary>
    /// Record a connection error.
    /// </summary>
    public void RecordError(Exception exception)
    {
        _consecutiveFailures++;
        _logger.LogWarning(exception, "Connection error recorded. Consecutive failures: {Count}",
            _consecutiveFailures);

        UpdateHealth(ConnectionHealth.Degraded);

        if (_options.AutoReconnect)
        {
            _ = TryReconnectAsync(exception);
        }
    }

    /// <summary>
    /// Record connection loss.
    /// </summary>
    public void RecordDisconnection()
    {
        UpdateHealth(ConnectionHealth.Disconnected);

        if (_options.AutoReconnect)
        {
            _ = TryReconnectAsync(new InvalidOperationException("Connection lost"));
        }
    }

    private async Task MonitorLoopAsync()
    {
        var checkInterval = TimeSpan.FromSeconds(Math.Max(5, _options.HeartbeatTimeout.TotalSeconds / 3));

        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(checkInterval, _cts.Token);

                var timeSinceActivity = DateTime.UtcNow - _lastActivityTime;

                if (timeSinceActivity > _options.HeartbeatTimeout)
                {
                    _logger.LogWarning(
                        "No activity for {Seconds:F1}s (timeout: {Timeout:F1}s). Connection may be stale.",
                        timeSinceActivity.TotalSeconds,
                        _options.HeartbeatTimeout.TotalSeconds);

                    UpdateHealth(ConnectionHealth.Stale);

                    if (_options.AutoReconnect)
                    {
                        await TryReconnectAsync(
                            new TimeoutException($"No activity for {timeSinceActivity.TotalSeconds:F1} seconds"));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in health monitor loop");
            }
        }
    }

    private async Task TryReconnectAsync(Exception triggerException)
    {
        // Ensure only one reconnection attempt at a time
        if (Interlocked.CompareExchange(ref _isReconnecting, 1, 0) != 0)
        {
            _logger.LogDebug("Reconnection already in progress, skipping");
            return;
        }

        try
        {
            UpdateHealth(ConnectionHealth.Reconnecting);

            for (int attempt = 0; _options.RetryPolicy.ShouldRetry(attempt); attempt++)
            {
                // Check if reconnection should proceed
                if (_options.OnReconnecting != null)
                {
                    try
                    {
                        if (!_options.OnReconnecting(attempt, triggerException))
                        {
                            _logger.LogInformation("Reconnection cancelled by callback");
                            UpdateHealth(ConnectionHealth.Disconnected);
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in OnReconnecting callback");
                    }
                }

                var delay = _options.RetryPolicy.GetDelay(attempt);
                _logger.LogInformation(
                    "Reconnection attempt {Attempt}/{Max} in {Delay:F1}s",
                    attempt + 1, _options.RetryPolicy.MaxRetries, delay.TotalSeconds);

                await Task.Delay(delay, _cts.Token);

                try
                {
                    await _reconnectAction(_cts.Token);

                    // Success!
                    ReconnectionCount++;
                    _consecutiveFailures = 0;
                    _lastActivityTime = DateTime.UtcNow;
                    UpdateHealth(ConnectionHealth.Healthy);

                    _logger.LogInformation(
                        "Reconnection successful on attempt {Attempt}. Total reconnections: {Total}",
                        attempt + 1, ReconnectionCount);

                    try
                    {
                        _options.OnReconnected?.Invoke(attempt + 1);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in OnReconnected callback");
                    }

                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Reconnection attempt {Attempt} failed", attempt + 1);
                }
            }

            // All retries exhausted
            _logger.LogError(
                "All {Max} reconnection attempts failed",
                _options.RetryPolicy.MaxRetries);

            UpdateHealth(ConnectionHealth.Failed);

            try
            {
                _options.OnReconnectFailed?.Invoke(triggerException);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnReconnectFailed callback");
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isReconnecting, 0);
        }
    }

    private void UpdateHealth(ConnectionHealth newHealth)
    {
        var oldHealth = CurrentHealth;
        if (oldHealth != newHealth)
        {
            CurrentHealth = newHealth;
            _logger.LogDebug("Connection health changed: {Old} -> {New}", oldHealth, newHealth);

            try
            {
                HealthChanged?.Invoke(this, new ConnectionHealthEventArgs(oldHealth, newHealth));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in HealthChanged event handler");
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}

/// <summary>
/// Connection health status.
/// </summary>
public enum ConnectionHealth
{
    /// <summary>Health status unknown (not started).</summary>
    Unknown,
    /// <summary>Connection is healthy and receiving data.</summary>
    Healthy,
    /// <summary>Connection experienced errors but is still active.</summary>
    Degraded,
    /// <summary>No activity received within heartbeat timeout.</summary>
    Stale,
    /// <summary>Currently attempting to reconnect.</summary>
    Reconnecting,
    /// <summary>Connection is disconnected.</summary>
    Disconnected,
    /// <summary>All reconnection attempts failed.</summary>
    Failed
}

/// <summary>
/// Event args for connection health changes.
/// </summary>
public sealed class ConnectionHealthEventArgs : EventArgs
{
    public ConnectionHealth PreviousHealth { get; }
    public ConnectionHealth CurrentHealth { get; }

    public ConnectionHealthEventArgs(ConnectionHealth previous, ConnectionHealth current)
    {
        PreviousHealth = previous;
        CurrentHealth = current;
    }
}
