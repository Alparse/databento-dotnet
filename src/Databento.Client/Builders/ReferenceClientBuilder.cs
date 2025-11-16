using Databento.Client.Models;
using Databento.Client.Reference;
using Microsoft.Extensions.Logging;

namespace Databento.Client.Builders;

/// <summary>
/// Builder for creating ReferenceClient instances
/// </summary>
public sealed class ReferenceClientBuilder
{
    private string? _apiKey;
    private HistoricalGateway _gateway = HistoricalGateway.Bo1;
    private ILogger<IReferenceClient>? _logger;

    /// <summary>
    /// Set the Databento API key
    /// </summary>
    /// <param name="apiKey">32-character API key starting with 'db-'</param>
    public ReferenceClientBuilder WithApiKey(string apiKey)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        return this;
    }

    /// <summary>
    /// Set the historical gateway to connect to
    /// </summary>
    /// <param name="gateway">Historical gateway (currently only Bo1 is supported)</param>
    public ReferenceClientBuilder WithGateway(HistoricalGateway gateway)
    {
        _gateway = gateway;
        return this;
    }

    /// <summary>
    /// Set the logger for operational diagnostics and debugging
    /// </summary>
    /// <param name="logger">Logger instance for the reference client (optional)</param>
    public ReferenceClientBuilder WithLogger(ILogger<IReferenceClient> logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// Build the ReferenceClient instance
    /// </summary>
    /// <returns>Configured ReferenceClient instance</returns>
    public IReferenceClient Build()
    {
        // Try environment variable if API key not provided
        if (string.IsNullOrEmpty(_apiKey))
        {
            _apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException(
                    "API key is required. Either call WithApiKey() or set the DATABENTO_API_KEY environment variable.");
            }
        }

        return new ReferenceClient(_apiKey, _gateway, _logger);
    }
}
