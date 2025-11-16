using Databento.Client.Models;
using Databento.Client.Models.Reference;

namespace Databento.Client.Reference;

/// <summary>
/// Security master reference API
/// </summary>
public interface ISecurityMasterApi
{
    /// <summary>
    /// Get the latest security master data
    /// </summary>
    /// <param name="symbols">Symbols to filter for (up to 2,000). Use "ALL_SYMBOLS" or null for all symbols.</param>
    /// <param name="stypeIn">Symbology type of input symbols (default: raw_symbol)</param>
    /// <param name="countries">Listing countries to filter for (ISO 3166-1 alpha-2)</param>
    /// <param name="securityTypes">Security types to filter for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of security master records</returns>
    Task<List<SecurityMasterRecord>> GetLastAsync(
        IEnumerable<string>? symbols = null,
        SType stypeIn = SType.RawSymbol,
        IEnumerable<string>? countries = null,
        IEnumerable<string>? securityTypes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get security master point-in-time (PIT) time series data
    /// </summary>
    /// <param name="start">Inclusive start of the request range</param>
    /// <param name="end">Exclusive end of the request range (null for all data after start)</param>
    /// <param name="index">Index column to use for filtering (ts_effective or ts_record)</param>
    /// <param name="symbols">Symbols to filter for (up to 2,000). Use "ALL_SYMBOLS" or null for all symbols.</param>
    /// <param name="stypeIn">Symbology type of input symbols (default: raw_symbol)</param>
    /// <param name="countries">Listing countries to filter for (ISO 3166-1 alpha-2)</param>
    /// <param name="securityTypes">Security types to filter for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of security master records</returns>
    Task<List<SecurityMasterRecord>> GetRangeAsync(
        DateTimeOffset start,
        DateTimeOffset? end = null,
        string index = "ts_effective",
        IEnumerable<string>? symbols = null,
        SType stypeIn = SType.RawSymbol,
        IEnumerable<string>? countries = null,
        IEnumerable<string>? securityTypes = null,
        CancellationToken cancellationToken = default);
}
