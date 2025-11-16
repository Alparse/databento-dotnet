using Databento.Client.Live;
using Databento.Client.Models;
using Microsoft.Extensions.Logging;

namespace Databento.Client.Builders;

/// <summary>
/// Builder for creating LiveBlockingClient instances (pull-based API)
/// </summary>
public sealed class LiveBlockingClientBuilder
{
    private string? _apiKey;
    private string? _dataset;
    private bool _sendTsOut = false;
    private VersionUpgradePolicy _upgradePolicy = VersionUpgradePolicy.Upgrade;
    private TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);
    private ILogger<ILiveBlockingClient>? _logger;

    /// <summary>
    /// Set the Databento API key
    /// </summary>
    public LiveBlockingClientBuilder WithApiKey(string apiKey)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        return this;
    }

    /// <summary>
    /// Set the default dataset for subscriptions
    /// </summary>
    /// <param name="dataset">Dataset name (e.g., "GLBX.MDP3", "XNAS.ITCH")</param>
    public LiveBlockingClientBuilder WithDataset(string dataset)
    {
        _dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
        return this;
    }

    /// <summary>
    /// Enable sending ts_out timestamps in records
    /// </summary>
    /// <param name="sendTsOut">True to include ts_out, false otherwise</param>
    public LiveBlockingClientBuilder WithSendTsOut(bool sendTsOut)
    {
        _sendTsOut = sendTsOut;
        return this;
    }

    /// <summary>
    /// Set the DBN version upgrade policy
    /// </summary>
    /// <param name="policy">Upgrade policy (AsIs or Upgrade)</param>
    public LiveBlockingClientBuilder WithUpgradePolicy(VersionUpgradePolicy policy)
    {
        _upgradePolicy = policy;
        return this;
    }

    /// <summary>
    /// Set the heartbeat interval for connection monitoring
    /// </summary>
    /// <param name="interval">Heartbeat interval</param>
    public LiveBlockingClientBuilder WithHeartbeatInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Heartbeat interval must be positive");

        _heartbeatInterval = interval;
        return this;
    }

    /// <summary>
    /// Set the logger for operational diagnostics and debugging
    /// </summary>
    /// <param name="logger">Logger instance for the live blocking client (optional)</param>
    /// <remarks>
    /// When provided, the client will log connection state changes, subscription operations,
    /// errors, and other diagnostic information. This is highly recommended for production deployments.
    /// </remarks>
    public LiveBlockingClientBuilder WithLogger(ILogger<ILiveBlockingClient> logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// Build the LiveBlockingClient instance
    /// </summary>
    public ILiveBlockingClient Build()
    {
        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("API key is required. Call WithApiKey() before Build().");

        return new LiveBlockingClient(
            _apiKey,
            _dataset,
            _sendTsOut,
            _upgradePolicy,
            _heartbeatInterval,
            _logger);
    }
}
