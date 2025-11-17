using System.Text.Json;

namespace Databento.Client.Reference;

/// <summary>
/// HIGH FIX: Shared helper methods for Reference APIs to eliminate code duplication.
/// Extracted from CorporateActionsApi, AdjustmentFactorsApi, and SecurityMasterApi.
/// </summary>
internal static class ReferenceApiHelpers
{
    /// <summary>
    /// Ensures the HTTP response has a success status code, otherwise throws an appropriate exception.
    /// </summary>
    /// <param name="response">The HTTP response message</param>
    /// <exception cref="Databento.Interop.ValidationException">Thrown for 4xx client errors</exception>
    /// <exception cref="Databento.Interop.ServerException">Thrown for 5xx server errors</exception>
    /// <exception cref="Databento.Interop.DbentoException">Thrown for other non-success status codes</exception>
    internal static async Task EnsureSuccessStatusCode(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
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

    /// <summary>
    /// Formats a DateTimeOffset as an ISO 8601 timestamp string for API requests.
    /// </summary>
    /// <param name="timestamp">The timestamp to format</param>
    /// <returns>ISO 8601 formatted string: yyyy-MM-ddTHH:mm:ss.fffffffZ</returns>
    internal static string FormatTimestamp(DateTimeOffset timestamp)
    {
        // Format as ISO 8601: yyyy-MM-ddTHH:mm:ss.fffffffZ
        return timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
    }

    /// <summary>
    /// Shared JSON serializer options for deserializing Reference API responses.
    /// Uses case-insensitive property matching and snake_case naming.
    /// </summary>
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}
