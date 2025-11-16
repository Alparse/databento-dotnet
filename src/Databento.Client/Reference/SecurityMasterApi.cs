using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Web;
using Databento.Client.Models;
using Databento.Client.Models.Reference;
using Microsoft.Extensions.Logging;
using Polly;

namespace Databento.Client.Reference;

/// <summary>
/// Security master reference API implementation
/// </summary>
internal sealed class SecurityMasterApi : ISecurityMasterApi
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger? _logger;
    private readonly Func<bool> _isDisposed;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    public SecurityMasterApi(HttpClient httpClient, string baseUrl, ILogger? logger, Func<bool> isDisposed, IAsyncPolicy<HttpResponseMessage> retryPolicy)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        _logger = logger;
        _isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
        _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
    }

    public async Task<List<SecurityMasterRecord>> GetLastAsync(
        IEnumerable<string>? symbols = null,
        SType stypeIn = SType.RawSymbol,
        IEnumerable<string>? countries = null,
        IEnumerable<string>? securityTypes = null,
        CancellationToken cancellationToken = default)
    {
        // HIGH FIX: Check disposal before HTTP operations
        ObjectDisposedException.ThrowIf(_isDisposed(), typeof(ReferenceClient));

        var queryParams = new Dictionary<string, string>();

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

        var url = $"{_baseUrl}/v0/security_master.get_last";
        _logger?.LogDebug("POST {Url}", url);

        // MEDIUM FIX: Add distributed tracing and metrics
        using var activity = Utilities.Telemetry.ActivitySource.StartActivity("security_master.get_last", ActivityKind.Client);
        activity?.SetTag("databento.api.endpoint", "security_master.get_last");
        activity?.SetTag("databento.api.method", "POST");

        var stopwatch = Stopwatch.StartNew();
        Utilities.Telemetry.ApiRequests.Add(1, new KeyValuePair<string, object?>("endpoint", "security_master.get_last"));

        try
        {
            // MEDIUM FIX: Execute with retry policy for transient failures
            var content = new FormUrlEncodedContent(queryParams);
            var response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _httpClient.PostAsync(url, content, cancellationToken);
            });
            await EnsureSuccessStatusCode(response);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var records = JsonSerializer.Deserialize<List<SecurityMasterRecord>>(json, JsonOptions);

            stopwatch.Stop();
            Utilities.Telemetry.ApiRequestDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("endpoint", "security_master.get_last"),
                new KeyValuePair<string, object?>("status", "success"));

            return records ?? new List<SecurityMasterRecord>();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Utilities.Telemetry.ApiRequestFailures.Add(1, new KeyValuePair<string, object?>("endpoint", "security_master.get_last"));
            Utilities.Telemetry.ApiRequestDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("endpoint", "security_master.get_last"),
                new KeyValuePair<string, object?>("status", "failure"));
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    public async Task<List<SecurityMasterRecord>> GetRangeAsync(
        DateTimeOffset start,
        DateTimeOffset? end = null,
        string index = "ts_effective",
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
            ["start"] = FormatTimestamp(start),
            ["index"] = index
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

        var url = $"{_baseUrl}/v0/security_master.get_range";
        _logger?.LogDebug("POST {Url}", url);

        // MEDIUM FIX: Execute with retry policy for transient failures
        var content = new FormUrlEncodedContent(queryParams);
        var response = await _retryPolicy.ExecuteAsync(async () =>
        {
            return await _httpClient.PostAsync(url, content, cancellationToken);
        });
        await EnsureSuccessStatusCode(response);

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var records = JsonSerializer.Deserialize<List<SecurityMasterRecord>>(json, JsonOptions);

        return records ?? new List<SecurityMasterRecord>();
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

        // Map HTTP status codes to appropriate exceptions
        var message = $"{statusCode} - {response.ReasonPhrase}\n{content}";

        if (statusCode >= 400 && statusCode < 500)
        {
            // Client error
            throw new Databento.Interop.ValidationException(message);
        }
        else if (statusCode >= 500)
        {
            // Server error
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
