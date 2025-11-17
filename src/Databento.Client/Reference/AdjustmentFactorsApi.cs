using System.Text.Json;
using System.Web;
using Databento.Client.Models;
using Databento.Client.Models.Reference;
using Microsoft.Extensions.Logging;
using Polly;

namespace Databento.Client.Reference;

/// <summary>
/// Adjustment factors reference API implementation
/// </summary>
internal sealed class AdjustmentFactorsApi : IAdjustmentFactorsApi
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger? _logger;
    private readonly Func<bool> _isDisposed;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public AdjustmentFactorsApi(HttpClient httpClient, string baseUrl, ILogger? logger, Func<bool> isDisposed, IAsyncPolicy<HttpResponseMessage> retryPolicy)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        _logger = logger;
        _isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
    }

    public async Task<List<AdjustmentFactorRecord>> GetRangeAsync(
        DateTimeOffset start,
        DateTimeOffset? end = null,
        IEnumerable<string>? symbols = null,
        SType stypeIn = SType.RawSymbol,
        IEnumerable<string>? countries = null,
        IEnumerable<string>? securityTypes = null,
        CancellationToken cancellationToken = default)
    {
        // HIGH FIX: Check disposal before HTTP operations
        ObjectDisposedException.ThrowIf(_isDisposed(), typeof(ReferenceClient));

        var queryParams = new Dictionary<string, string>
        {
            ["start"] = ReferenceApiHelpers.FormatTimestamp(start)
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

        // Add countries parameter
        if (countries != null)
        {
            var countryList = countries.ToList();
            if (countryList.Count > 0)
            {
                queryParams["countries"] = string.Join(",", countryList);
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

        var url = $"{_baseUrl}/v0/adjustment_factors.get_range";
        _logger?.LogDebug("POST {Url}", url);

        // MEDIUM FIX: Execute with retry policy for transient failures
        var content = new FormUrlEncodedContent(queryParams);
        var response = await _retryPolicy.ExecuteAsync(async () =>
        {
            return await _httpClient.PostAsync(url, content, cancellationToken);
        });
        await ReferenceApiHelpers.EnsureSuccessStatusCode(response).ConfigureAwait(false);

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var records = JsonSerializer.Deserialize<List<AdjustmentFactorRecord>>(json, ReferenceApiHelpers.JsonOptions);

        return records ?? new List<AdjustmentFactorRecord>();
    }
}
