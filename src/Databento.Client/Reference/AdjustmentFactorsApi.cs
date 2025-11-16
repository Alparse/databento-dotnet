using System.Text.Json;
using System.Web;
using Databento.Client.Models;
using Databento.Client.Models.Reference;
using Microsoft.Extensions.Logging;

namespace Databento.Client.Reference;

/// <summary>
/// Adjustment factors reference API implementation
/// </summary>
internal sealed class AdjustmentFactorsApi : IAdjustmentFactorsApi
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger? _logger;

    public AdjustmentFactorsApi(HttpClient httpClient, string baseUrl, ILogger? logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        _logger = logger;
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
        var queryParams = new Dictionary<string, string>
        {
            ["start"] = FormatTimestamp(start)
        };

        if (end.HasValue)
        {
            queryParams["end"] = FormatTimestamp(end.Value);
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

        var content = new FormUrlEncodedContent(queryParams);
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        await EnsureSuccessStatusCode(response);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var records = JsonSerializer.Deserialize<List<AdjustmentFactorRecord>>(json, JsonOptions);

        return records ?? new List<AdjustmentFactorRecord>();
    }

    private static string BuildUrl(string baseUrl, Dictionary<string, string> queryParams)
    {
        if (queryParams.Count == 0)
        {
            return baseUrl;
        }

        var query = string.Join("&", queryParams.Select(kvp =>
            $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));

        return $"{baseUrl}?{query}";
    }

    private static string FormatTimestamp(DateTimeOffset timestamp)
    {
        // Format as ISO 8601: yyyy-MM-ddTHH:mm:ss.fffffffZ
        return timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
    }

    private static async Task EnsureSuccessStatusCode(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        var statusCode = (int)response.StatusCode;

        var message = $"{statusCode} - {response.ReasonPhrase}\n{content}";

        if (statusCode >= 400 && statusCode < 500)
        {
            throw new Databento.Interop.ValidationException(message);
        }
        else if (statusCode >= 500)
        {
            throw new Databento.Interop.ServerException(message);
        }
        else
        {
            throw new Databento.Interop.DbentoException(message, statusCode);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}
