using Databento.Client.Live;
using Databento.Client.Models;
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

    /// <summary>
    /// Set the Databento API key
    /// </summary>
    public LiveClientBuilder WithApiKey(string apiKey)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
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
            _exceptionHandler);
    }
}
