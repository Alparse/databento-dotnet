using Databento.Client.Models;
using Databento.Client.Models.Reference;

namespace Databento.Client.Reference;

/// <summary>
/// Corporate actions reference API
/// </summary>
public interface ICorporateActionsApi
{
    /// <summary>
    /// Get corporate actions time series data
    /// </summary>
    /// <param name="start">Inclusive start of the request range</param>
    /// <param name="end">Exclusive end of the request range (null for all data after start)</param>
    /// <param name="index">Index column to use for filtering (event_date, ex_date, or ts_record)</param>
    /// <param name="symbols">Symbols to filter for (up to 2,000). Use "ALL_SYMBOLS" or null for all symbols.</param>
    /// <param name="stypeIn">Symbology type of input symbols (default: raw_symbol)</param>
    /// <param name="events">Event types to filter for (null for all events)</param>
    /// <param name="countries">Listing countries to filter for (ISO 3166-1 alpha-2)</param>
    /// <param name="exchanges">Exchanges to filter for (null for all exchanges)</param>
    /// <param name="securityTypes">Security types to filter for (null for all security types)</param>
    /// <param name="flatten">If nested JSON objects should be flattened (default: true)</param>
    /// <param name="pit">Point-in-time mode - retain all historical records (default: false)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of corporate action records</returns>
    Task<List<CorporateActionRecord>> GetRangeAsync(
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
        CancellationToken cancellationToken = default);
}
