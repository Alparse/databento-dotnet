using System.IO.Compression;
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

    /// <summary>
    /// Parses a JSONL (JSON Lines) response into a list of records.
    /// The Databento Reference API returns JSONL format (one JSON object per line),
    /// optionally compressed with zstd.
    /// </summary>
    /// <typeparam name="T">The record type to deserialize</typeparam>
    /// <param name="response">The HTTP response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of deserialized records</returns>
    internal static async Task<List<T>> ParseJsonLinesResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        var records = new List<T>();

        // Get the response stream
        using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        // Check if the response is zstd compressed
        Stream dataStream = responseStream;
        var contentEncoding = response.Content.Headers.ContentEncoding.FirstOrDefault();

        if (string.Equals(contentEncoding, "zstd", StringComparison.OrdinalIgnoreCase))
        {
            // Decompress zstd - use ZstdSharp or read all bytes and decompress
            // For now, read all bytes since .NET doesn't have built-in zstd
            var compressedBytes = await ReadAllBytesAsync(responseStream, cancellationToken).ConfigureAwait(false);
            var decompressedBytes = DecompressZstd(compressedBytes);
            dataStream = new MemoryStream(decompressedBytes);
        }

        // Parse JSONL (one JSON object per line)
        using var reader = new StreamReader(dataStream);
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var record = JsonSerializer.Deserialize<T>(line, JsonOptions);
            if (record != null)
            {
                records.Add(record);
            }
        }

        return records;
    }

    /// <summary>
    /// Reads all bytes from a stream asynchronously.
    /// </summary>
    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Decompresses zstd-compressed data.
    /// </summary>
    private static byte[] DecompressZstd(byte[] compressedData)
    {
        // Use ZstdSharp for decompression
        using var decompressor = new ZstdSharp.Decompressor();
        return decompressor.Unwrap(compressedData).ToArray();
    }
}
