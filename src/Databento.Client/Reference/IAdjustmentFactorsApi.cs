using Databento.Client.Models;
using Databento.Client.Models.Reference;

namespace Databento.Client.Reference;

/// <summary>
/// Adjustment factors reference API
/// </summary>
public interface IAdjustmentFactorsApi
{
    /// <summary>
    /// Get adjustment factors time series data
    /// </summary>
    /// <param name="start">Inclusive start of the request range based on ex_date</param>
    /// <param name="end">Exclusive end of the request range (null for all data after start)</param>
    /// <param name="symbols">Symbols to filter for (up to 2,000). Use "ALL_SYMBOLS" or null for all symbols.</param>
    /// <param name="stypeIn">Symbology type of input symbols (default: raw_symbol)</param>
    /// <param name="countries">Listing countries to filter for (ISO 3166-1 alpha-2)</param>
    /// <param name="securityTypes">Security types to filter for (null for all security types)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of adjustment factor records</returns>
    Task<List<AdjustmentFactorRecord>> GetRangeAsync(
        DateTimeOffset start,
        DateTimeOffset? end = null,
        IEnumerable<string>? symbols = null,
        SType stypeIn = SType.RawSymbol,
        IEnumerable<string>? countries = null,
        IEnumerable<string>? securityTypes = null,
        CancellationToken cancellationToken = default);
}
