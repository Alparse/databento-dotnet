using System.Net.Http.Headers;
using System.Text;
using Databento.Client.Models;
using Microsoft.Extensions.Logging;

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
        : this(apiKey, HistoricalGateway.Bo1, null)
    {
    }

    internal ReferenceClient(
        string apiKey,
        HistoricalGateway gateway,
        ILogger<IReferenceClient>? logger)
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

        // Create HTTP client with authentication
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{apiKey}:")));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // 5-minute timeout for large responses

        // Initialize sub-APIs lazily
        _corporateActions = new Lazy<ICorporateActionsApi>(() =>
            new CorporateActionsApi(_httpClient, _baseUrl, _logger));
        _adjustmentFactors = new Lazy<IAdjustmentFactorsApi>(() =>
            new AdjustmentFactorsApi(_httpClient, _baseUrl, _logger));
        _securityMaster = new Lazy<ISecurityMasterApi>(() =>
            new SecurityMasterApi(_httpClient, _baseUrl, _logger));

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
