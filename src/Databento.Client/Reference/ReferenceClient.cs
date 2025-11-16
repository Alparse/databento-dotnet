using System.Net.Http.Headers;
using System.Text;
using Databento.Client.Models;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace Databento.Client.Reference;

/// <summary>
/// Reference data client for querying corporate actions, adjustment factors, and security master data
/// </summary>
public sealed class ReferenceClient : IReferenceClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly ILogger<IReferenceClient>? _logger;
    private bool _disposed;

    // MEDIUM FIX: Polly retry policy for transient HTTP failures
    private static readonly IAsyncPolicy<HttpResponseMessage> RetryPolicy =
        HttpPolicyExtensions
            .HandleTransientHttpError() // Handles 5xx and 408 errors
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests) // Handle 429 rate limiting
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff: 2s, 4s, 8s
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Log retry attempts (optional - would need logger instance)
                    // This is a static policy, so we can't access instance logger here
                    // Logging will be done in the sub-APIs where we have access to logger
                });

    // Sub-APIs
    private readonly Lazy<ICorporateActionsApi> _corporateActions;
    private readonly Lazy<IAdjustmentFactorsApi> _adjustmentFactors;
    private readonly Lazy<ISecurityMasterApi> _securityMaster;

    /// <summary>
    /// Corporate actions API
    /// </summary>
    public ICorporateActionsApi CorporateActions => _corporateActions.Value;

    /// <summary>
    /// Adjustment factors API
    /// </summary>
    public IAdjustmentFactorsApi AdjustmentFactors => _adjustmentFactors.Value;

    /// <summary>
    /// Security master API
    /// </summary>
    public ISecurityMasterApi SecurityMaster => _securityMaster.Value;

    internal ReferenceClient(string apiKey)
        : this(apiKey, HistoricalGateway.Bo1, null, null)
    {
    }

    internal ReferenceClient(
        string apiKey,
        HistoricalGateway gateway,
        ILogger<IReferenceClient>? logger)
        : this(apiKey, gateway, logger, null)
    {
    }

    /// <summary>
    /// Internal constructor accepting a pre-configured HttpClient
    /// </summary>
    /// <param name="apiKey">API key</param>
    /// <param name="gateway">Historical gateway</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="httpClient">Pre-configured HttpClient (if null, creates new instance)</param>
    internal ReferenceClient(
        string apiKey,
        HistoricalGateway gateway,
        ILogger<IReferenceClient>? logger,
        HttpClient? httpClient)
    {
        if (string.IsNullOrEmpty(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

        _apiKey = apiKey;
        _logger = logger;

        // Map gateway to base URL
        _baseUrl = gateway switch
        {
            HistoricalGateway.Bo1 => "https://hist.databento.com",
            HistoricalGateway.Bo2 => "https://hist.databento.com", // Same URL for now
            _ => throw new ArgumentException($"Unsupported gateway: {gateway}", nameof(gateway))
        };

        // HIGH FIX: Accept HttpClient via DI or create with proper configuration
        // If httpClient provided, use it; otherwise create new instance
        // This allows proper HttpClient management via IHttpClientFactory or manual reuse
        if (httpClient != null)
        {
            _httpClient = httpClient;
        }
        else
        {
            // Create HTTP client with authentication
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{apiKey}:")));
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.Timeout = TimeSpan.FromMinutes(5); // 5-minute timeout for large responses
        }

        // Initialize sub-APIs lazily with retry policy
        _corporateActions = new Lazy<ICorporateActionsApi>(() =>
            new CorporateActionsApi(_httpClient, _baseUrl, _logger, () => _disposed, RetryPolicy));
        _adjustmentFactors = new Lazy<IAdjustmentFactorsApi>(() =>
            new AdjustmentFactorsApi(_httpClient, _baseUrl, _logger, () => _disposed, RetryPolicy));
        _securityMaster = new Lazy<ISecurityMasterApi>(() =>
            new SecurityMasterApi(_httpClient, _baseUrl, _logger, () => _disposed, RetryPolicy));

        _logger?.LogInformation(
            "ReferenceClient created successfully. Gateway={Gateway}, BaseUrl={BaseUrl}",
            gateway,
            _baseUrl);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _httpClient?.Dispose();
        _disposed = true;

        await Task.CompletedTask;
    }
}
