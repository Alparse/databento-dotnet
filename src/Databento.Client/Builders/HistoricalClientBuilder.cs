using Databento.Client.Historical;
using Databento.Client.Models;
using Microsoft.Extensions.Logging;

namespace Databento.Client.Builders;

/// <summary>
/// Builder for creating HistoricalClient instances
/// </summary>
public sealed class HistoricalClientBuilder
{
    private string? _apiKey;
    private HistoricalGateway _gateway = HistoricalGateway.Bo1;
    private string? _customHost;
    private ushort? _customPort;
    private VersionUpgradePolicy _upgradePolicy = VersionUpgradePolicy.Upgrade;
    private string? _userAgent;
    private TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private ILogger<IHistoricalClient>? _logger;

    /// <summary>
    /// Set the Databento API key
    /// </summary>
    public HistoricalClientBuilder WithApiKey(string apiKey)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        return this;
    }

    /// <summary>
    /// Set the API key from the DATABENTO_API_KEY environment variable
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the environment variable is not set</exception>
    public HistoricalClientBuilder WithKeyFromEnv()
    {
        _apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
            ?? throw new InvalidOperationException(
                "DATABENTO_API_KEY environment variable is not set. " +
                "Set the environment variable or use WithApiKey() instead.");
        return this;
    }

    /// <summary>
    /// Set the historical API gateway
    /// </summary>
    /// <param name="gateway">Gateway to use (Bo1, Bo2, or Custom)</param>
    public HistoricalClientBuilder WithGateway(HistoricalGateway gateway)
    {
        _gateway = gateway;
        return this;
    }

    /// <summary>
    /// Set a custom gateway address (requires HistoricalGateway.Custom)
    /// </summary>
    /// <param name="host">Hostname or IP address</param>
    /// <param name="port">Port number</param>
    public HistoricalClientBuilder WithAddress(string host, ushort port)
    {
        _customHost = host ?? throw new ArgumentNullException(nameof(host));
        _customPort = port;
        _gateway = HistoricalGateway.Custom;
        return this;
    }

    /// <summary>
    /// Set the DBN version upgrade policy
    /// </summary>
    /// <param name="policy">Upgrade policy (AsIs or Upgrade)</param>
    public HistoricalClientBuilder WithUpgradePolicy(VersionUpgradePolicy policy)
    {
        _upgradePolicy = policy;
        return this;
    }

    /// <summary>
    /// Extend the User-Agent header sent with requests
    /// </summary>
    /// <param name="userAgent">Additional user agent string</param>
    public HistoricalClientBuilder WithUserAgent(string userAgent)
    {
        _userAgent = userAgent;
        return this;
    }

    /// <summary>
    /// Set the request timeout
    /// </summary>
    /// <param name="timeout">Timeout duration</param>
    public HistoricalClientBuilder WithTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive");

        _timeout = timeout;
        return this;
    }

    /// <summary>
    /// Set the logger for operational diagnostics and debugging
    /// </summary>
    /// <param name="logger">Logger instance for the historical client (optional)</param>
    /// <remarks>
    /// When provided, the client will log query operations, errors, and other diagnostic information.
    /// This is highly recommended for production deployments to troubleshoot issues and monitor performance.
    /// </remarks>
    public HistoricalClientBuilder WithLogger(ILogger<IHistoricalClient> logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// Build the HistoricalClient instance
    /// </summary>
    public IHistoricalClient Build()
    {
        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("API key is required. Call WithApiKey() before Build().");

        if (_gateway == HistoricalGateway.Custom && (string.IsNullOrEmpty(_customHost) || !_customPort.HasValue))
            throw new InvalidOperationException("Custom gateway requires host and port. Call WithAddress() or use a standard gateway.");

        return new HistoricalClient(
            _apiKey,
            _gateway,
            _customHost,
            _customPort,
            _upgradePolicy,
            _userAgent,
            _timeout,
            _logger);
    }
}
