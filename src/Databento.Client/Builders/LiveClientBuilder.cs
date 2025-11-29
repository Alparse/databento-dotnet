using Databento.Client.Live;
using Databento.Client.Models;
using Databento.Client.Resilience;
using Microsoft.Extensions.Logging;

namespace Databento.Client.Builders;

/// <summary>
/// Builder for creating LiveClient instances
/// </summary>
public sealed class LiveClientBuilder
{
    private string? _apiKey;
    private string? _dataset;
    private bool _sendTsOut = false;
    private VersionUpgradePolicy _upgradePolicy = VersionUpgradePolicy.Upgrade;
    private TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);
    private ILogger<ILiveClient>? _logger;
    private ExceptionCallback? _exceptionHandler;
    private ResilienceOptions _resilienceOptions = new();

    /// <summary>
    /// Set the Databento API key
    /// </summary>
    public LiveClientBuilder WithApiKey(string apiKey)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        return this;
    }

    /// <summary>
    /// Set the API key from the DATABENTO_API_KEY environment variable
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the environment variable is not set</exception>
    public LiveClientBuilder WithKeyFromEnv()
    {
        _apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
            ?? throw new InvalidOperationException(
                "DATABENTO_API_KEY environment variable is not set. " +
                "Set the environment variable or use WithApiKey() instead.");
        return this;
    }

    /// <summary>
    /// Set the default dataset for subscriptions
    /// </summary>
    /// <param name="dataset">Dataset name (e.g., "GLBX.MDP3", "XNAS.ITCH")</param>
    public LiveClientBuilder WithDataset(string dataset)
    {
        _dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
        return this;
    }

    /// <summary>
    /// Enable sending ts_out timestamps in records
    /// </summary>
    /// <param name="sendTsOut">True to include ts_out, false otherwise</param>
    public LiveClientBuilder WithSendTsOut(bool sendTsOut)
    {
        _sendTsOut = sendTsOut;
        return this;
    }

    /// <summary>
    /// Set the DBN version upgrade policy
    /// </summary>
    /// <param name="policy">Upgrade policy (AsIs or Upgrade)</param>
    public LiveClientBuilder WithUpgradePolicy(VersionUpgradePolicy policy)
    {
        _upgradePolicy = policy;
        return this;
    }

    /// <summary>
    /// Set the heartbeat interval for connection monitoring
    /// </summary>
    /// <param name="interval">Heartbeat interval</param>
    public LiveClientBuilder WithHeartbeatInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Heartbeat interval must be positive");

        _heartbeatInterval = interval;
        return this;
    }

    /// <summary>
    /// Set the logger for operational diagnostics and debugging
    /// </summary>
    /// <param name="logger">Logger instance for the live client (optional)</param>
    /// <remarks>
    /// When provided, the client will log connection state changes, subscription operations,
    /// errors, and other diagnostic information. This is highly recommended for production deployments.
    /// </remarks>
    public LiveClientBuilder WithLogger(ILogger<ILiveClient> logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// Set the exception handler callback (matches databento-cpp ExceptionCallback)
    /// </summary>
    /// <param name="exceptionHandler">Callback to handle exceptions during streaming</param>
    /// <remarks>
    /// The exception handler is called when errors occur during streaming. Return ExceptionAction.Continue
    /// to continue processing, or ExceptionAction.Stop to terminate the stream.
    /// If no handler is provided, exceptions will only be raised via the ErrorOccurred event.
    /// Matches C++ API: void Start(metadata_callback, record_callback, exception_callback);
    /// </remarks>
    public LiveClientBuilder WithExceptionHandler(ExceptionCallback exceptionHandler)
    {
        _exceptionHandler = exceptionHandler;
        return this;
    }

    /// <summary>
    /// Enable automatic reconnection on connection failure.
    /// Uses the default retry policy (3 retries with exponential backoff).
    /// </summary>
    /// <param name="enabled">True to enable auto-reconnect, false to disable</param>
    public LiveClientBuilder WithAutoReconnect(bool enabled = true)
    {
        _resilienceOptions.AutoReconnect = enabled;
        return this;
    }

    /// <summary>
    /// Configure the retry policy for connection attempts.
    /// </summary>
    /// <param name="policy">The retry policy to use</param>
    public LiveClientBuilder WithRetryPolicy(RetryPolicy policy)
    {
        _resilienceOptions.RetryPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    /// <summary>
    /// Configure the heartbeat timeout for detecting stale connections.
    /// If no activity is received within this duration, the connection is considered stale.
    /// </summary>
    /// <param name="timeout">The heartbeat timeout (default: 90 seconds)</param>
    public LiveClientBuilder WithHeartbeatTimeout(TimeSpan timeout)
    {
        if (timeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout cannot be negative");

        _resilienceOptions.HeartbeatTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Configure full resilience options.
    /// </summary>
    /// <param name="options">The resilience options to use</param>
    public LiveClientBuilder WithResilienceOptions(ResilienceOptions options)
    {
        _resilienceOptions = options ?? throw new ArgumentNullException(nameof(options));
        return this;
    }

    /// <summary>
    /// Build the LiveClient instance
    /// </summary>
    public ILiveClient Build()
    {
        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("API key is required. Call WithApiKey() before Build().");

        return new LiveClient(
            _apiKey,
            _dataset,
            _sendTsOut,
            _upgradePolicy,
            _heartbeatInterval,
            _logger,
            _exceptionHandler,
            _resilienceOptions);
    }
}
