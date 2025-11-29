using System.Text.Json;
using System.Web;
using Databento.Client.Models;
using Databento.Client.Models.Reference;
using Microsoft.Extensions.Logging;
using Polly;

namespace Databento.Client.Reference;

/// <summary>
/// Corporate actions reference API implementation
/// </summary>
internal sealed class CorporateActionsApi : ICorporateActionsApi
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger _logger;
    private readonly Func<bool> _isDisposed;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public CorporateActionsApi(HttpClient httpClient, string baseUrl, ILogger logger, Func<bool> isDisposed, IAsyncPolicy<HttpResponseMessage> retryPolicy)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
    }

    public async Task<List<CorporateActionRecord>> GetRangeAsync(
        DateTimeOffset start,
        DateTimeOffset? end = null,
        string index = "event_date",
        IEnumerable<string>? symbols = null,
        SType stypeIn = SType.RawSymbol,
        IEnumerable<string>? events = null,
        IEnumerable<string>? countries = null,
        IEnumerable<string>? exchanges = null,
        IEnumerable<string>? securityTypes = null,
        bool flatten = true,
        bool pit = false,
        CancellationToken cancellationToken = default)
    {
        // HIGH FIX: Check disposal before HTTP operations
        ObjectDisposedException.ThrowIf(_isDisposed(), typeof(ReferenceClient));

        var queryParams = new Dictionary<string, string>
        {
            ["start"] = ReferenceApiHelpers.FormatTimestamp(start),
            ["index"] = index,
            ["flatten"] = flatten.ToString().ToLowerInvariant(),
            ["pit"] = pit.ToString().ToLowerInvariant()
        };

        if (end.HasValue)
        {
            queryParams["end"] = ReferenceApiHelpers.FormatTimestamp(end.Value);
        }

        // Add symbols parameter
        if (symbols != null)
        {
            var symbolList = symbols.ToList();
            if (symbolList.Count > 0)
            {
                queryParams["symbols"] = string.Join(",", symbolList);
            }
        }

        // Add stype_in parameter
        queryParams["stype_in"] = stypeIn.ToStypeString();

        // Add events parameter
        if (events != null)
        {
            var eventList = events.ToList();
            if (eventList.Count > 0)
            {
                queryParams["events"] = string.Join(",", eventList);
            }
        }

        // Add countries parameter
        if (countries != null)
        {
            var countryList = countries.ToList();
            if (countryList.Count > 0)
            {
                queryParams["countries"] = string.Join(",", countryList);
            }
        }

        // Add exchanges parameter
        if (exchanges != null)
        {
            var exchangeList = exchanges.ToList();
            if (exchangeList.Count > 0)
            {
                queryParams["exchanges"] = string.Join(",", exchangeList);
            }
        }

        // Add security_types parameter
        if (securityTypes != null)
        {
            var typeList = securityTypes.ToList();
            if (typeList.Count > 0)
            {
                queryParams["security_types"] = string.Join(",", typeList);
            }
        }

        var url = $"{_baseUrl}/v0/corporate_actions.get_range";
        _logger.LogDebug("POST {Url}", url);

        // MEDIUM FIX: Execute with retry policy for transient failures
        var content = new FormUrlEncodedContent(queryParams);
        var response = await _retryPolicy.ExecuteAsync(async () =>
        {
            return await _httpClient.PostAsync(url, content, cancellationToken);
        });
        await ReferenceApiHelpers.EnsureSuccessStatusCode(response).ConfigureAwait(false);

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var records = JsonSerializer.Deserialize<List<CorporateActionRecord>>(json, ReferenceApiHelpers.JsonOptions);

        return records ?? new List<CorporateActionRecord>();
    }
}
