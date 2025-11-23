using Databento.Client.Metadata;
using Databento.Client.Models;
using Databento.Client.Models.Batch;
using Databento.Client.Models.Metadata;
using Databento.Client.Models.Symbology;

namespace Databento.Client.Historical;

/// <summary>
/// Historical data client for querying past market data
/// </summary>
public interface IHistoricalClient : IAsyncDisposable
{
    /// <summary>
    /// Query historical data for a time range
    /// </summary>
    /// <param name="dataset">Dataset name (e.g., "GLBX.MDP3")</param>
    /// <param name="schema">Schema type</param>
    /// <param name="symbols">List of symbols</param>
    /// <param name="startTime">Start time</param>
    /// <param name="endTime">End time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of records</returns>
    IAsyncEnumerable<Record> GetRangeAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query historical data for a time range with symbology type filtering
    /// </summary>
    /// <param name="dataset">Dataset name (e.g., "GLBX.MDP3")</param>
    /// <param name="schema">Schema type</param>
    /// <param name="symbols">List of symbols</param>
    /// <param name="startTime">Start time</param>
    /// <param name="endTime">End time</param>
    /// <param name="stypeIn">Input symbology type (how symbols are interpreted)</param>
    /// <param name="stypeOut">Output symbology type (how symbols are returned in data)</param>
    /// <param name="limit">Maximum number of records to return (0 = unlimited)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of records</returns>
    IAsyncEnumerable<Record> GetRangeAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        SType stypeIn,
        SType stypeOut,
        ulong limit = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query historical data and save directly to a DBN file
    /// </summary>
    /// <param name="filePath">Output file path for DBN file</param>
    /// <param name="dataset">Dataset name (e.g., "GLBX.MDP3")</param>
    /// <param name="schema">Schema type</param>
    /// <param name="symbols">List of symbols</param>
    /// <param name="startTime">Start time</param>
    /// <param name="endTime">End time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Path to the created DBN file</returns>
    Task<string> GetRangeToFileAsync(
        string filePath,
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query historical data with symbology type filtering and save directly to a DBN file
    /// </summary>
    /// <param name="filePath">Output file path for DBN file</param>
    /// <param name="dataset">Dataset name (e.g., "GLBX.MDP3")</param>
    /// <param name="schema">Schema type</param>
    /// <param name="symbols">List of symbols</param>
    /// <param name="startTime">Start time</param>
    /// <param name="endTime">End time</param>
    /// <param name="stypeIn">Input symbology type (how symbols are interpreted)</param>
    /// <param name="stypeOut">Output symbology type (how symbols are returned in data)</param>
    /// <param name="limit">Maximum number of records to return (0 = unlimited)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Path to the created DBN file</returns>
    Task<string> GetRangeToFileAsync(
        string filePath,
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        SType stypeIn,
        SType stypeOut,
        ulong limit = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get metadata for a historical query
    /// Note: This feature is currently not fully implemented in the native layer
    /// </summary>
    /// <param name="dataset">Dataset name</param>
    /// <param name="schema">Schema type</param>
    /// <param name="startTime">Start time</param>
    /// <param name="endTime">End time</param>
    /// <returns>Metadata object, or null if not available</returns>
    IMetadata? GetMetadata(
        string dataset,
        Schema schema,
        DateTimeOffset startTime,
        DateTimeOffset endTime);

    // ========================================================================
    // Metadata API Methods
    // ========================================================================

    /// <summary>
    /// List all publishers
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of publisher details</returns>
    Task<IReadOnlyList<PublisherDetail>> ListPublishersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List datasets, optionally filtered by venue
    /// </summary>
    /// <param name="venue">Optional venue filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of dataset codes</returns>
    Task<IReadOnlyList<string>> ListDatasetsAsync(
        string? venue = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List schemas available for a dataset
    /// </summary>
    /// <param name="dataset">Dataset name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available schemas</returns>
    Task<IReadOnlyList<Schema>> ListSchemasAsync(
        string dataset,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List fields for a given encoding and schema
    /// </summary>
    /// <param name="encoding">Data encoding format</param>
    /// <param name="schema">Schema type</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of field details</returns>
    Task<IReadOnlyList<FieldDetail>> ListFieldsAsync(
        Encoding encoding,
        Schema schema,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get dataset availability condition
    /// </summary>
    /// <param name="dataset">Dataset name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dataset condition information</returns>
    Task<DatasetConditionInfo> GetDatasetConditionAsync(
        string dataset,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get dataset condition for a specific date range
    /// </summary>
    /// <param name="dataset">Dataset name</param>
    /// <param name="startDate">Start date (inclusive)</param>
    /// <param name="endDate">End date (inclusive, optional - if not specified, returns through last available date)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of condition details per date</returns>
    Task<IReadOnlyList<DatasetConditionDetail>> GetDatasetConditionAsync(
        string dataset,
        DateTimeOffset startDate,
        DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get dataset time range
    /// </summary>
    /// <param name="dataset">Dataset name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dataset time range</returns>
    Task<DatasetRange> GetDatasetRangeAsync(
        string dataset,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List unit prices per schema for all feed modes
    /// </summary>
    /// <param name="dataset">Dataset name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of unit prices for each feed mode</returns>
    Task<IReadOnlyList<UnitPricesForMode>> ListUnitPricesAsync(
        string dataset,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get record count for a query
    /// </summary>
    /// <param name="dataset">Dataset name</param>
    /// <param name="schema">Schema type</param>
    /// <param name="startTime">Start time</param>
    /// <param name="endTime">End time</param>
    /// <param name="symbols">List of symbols</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Record count</returns>
    Task<ulong> GetRecordCountAsync(
        string dataset,
        Schema schema,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get billable size for a query
    /// </summary>
    /// <param name="dataset">Dataset name</param>
    /// <param name="schema">Schema type</param>
    /// <param name="startTime">Start time</param>
    /// <param name="endTime">End time</param>
    /// <param name="symbols">List of symbols</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Billable size in bytes</returns>
    Task<ulong> GetBillableSizeAsync(
        string dataset,
        Schema schema,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get cost estimate for a query
    /// </summary>
    /// <param name="dataset">Dataset name</param>
    /// <param name="schema">Schema type</param>
    /// <param name="startTime">Start time</param>
    /// <param name="endTime">End time</param>
    /// <param name="symbols">List of symbols</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cost in USD</returns>
    Task<decimal> GetCostAsync(
        string dataset,
        Schema schema,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get combined billing information for a query
    /// </summary>
    /// <param name="dataset">Dataset name</param>
    /// <param name="schema">Schema type</param>
    /// <param name="startTime">Start time</param>
    /// <param name="endTime">End time</param>
    /// <param name="symbols">List of symbols</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Billing information</returns>
    Task<BillingInfo> GetBillingInfoAsync(
        string dataset,
        Schema schema,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    // ========================================================================
    // Batch API Methods
    // ========================================================================

    /// <summary>
    /// Submit a new batch job for bulk historical data download.
    /// WARNING: This operation will incur a cost.
    /// </summary>
    /// <param name="dataset">Dataset name</param>
    /// <param name="symbols">List of symbols</param>
    /// <param name="schema">Schema type</param>
    /// <param name="startTime">Start time</param>
    /// <param name="endTime">End time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Description of the submitted batch job</returns>
    Task<BatchJob> BatchSubmitJobAsync(
        string dataset,
        IEnumerable<string> symbols,
        Schema schema,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submit a new batch job with advanced options for bulk historical data download.
    /// WARNING: This operation will incur a cost.
    /// </summary>
    /// <param name="dataset">Dataset name</param>
    /// <param name="symbols">List of symbols</param>
    /// <param name="schema">Schema type</param>
    /// <param name="startTime">Start time</param>
    /// <param name="endTime">End time</param>
    /// <param name="encoding">Output encoding format</param>
    /// <param name="compression">Compression algorithm</param>
    /// <param name="prettyPx">Use human-readable prices</param>
    /// <param name="prettyTs">Use human-readable timestamps</param>
    /// <param name="mapSymbols">Include symbol mappings</param>
    /// <param name="splitSymbols">Split output by symbol</param>
    /// <param name="splitDuration">Split output by time duration</param>
    /// <param name="splitSize">Split output by size (bytes)</param>
    /// <param name="delivery">Delivery method</param>
    /// <param name="stypeIn">Input symbology type</param>
    /// <param name="stypeOut">Output symbology type</param>
    /// <param name="limit">Maximum number of records (0 = unlimited)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Description of the submitted batch job</returns>
    Task<BatchJob> BatchSubmitJobAsync(
        string dataset,
        IEnumerable<string> symbols,
        Schema schema,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        Encoding encoding,
        Compression compression,
        bool prettyPx,
        bool prettyTs,
        bool mapSymbols,
        bool splitSymbols,
        SplitDuration splitDuration,
        ulong splitSize,
        Delivery delivery,
        SType stypeIn,
        SType stypeOut,
        ulong limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List previous batch jobs
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of batch jobs</returns>
    Task<IReadOnlyList<BatchJob>> BatchListJobsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List previous batch jobs filtered by state and date
    /// </summary>
    /// <param name="states">Filter by job states</param>
    /// <param name="since">Only include jobs received since this time</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of batch jobs</returns>
    Task<IReadOnlyList<BatchJob>> BatchListJobsAsync(
        IEnumerable<JobState> states,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all files associated with a batch job
    /// </summary>
    /// <param name="jobId">Batch job identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of batch file descriptions</returns>
    Task<IReadOnlyList<BatchFileDesc>> BatchListFilesAsync(
        string jobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Download all files from a batch job to a directory
    /// </summary>
    /// <param name="outputDir">Output directory path</param>
    /// <param name="jobId">Batch job identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of downloaded file paths</returns>
    Task<IReadOnlyList<string>> BatchDownloadAsync(
        string outputDir,
        string jobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Download a specific file from a batch job
    /// </summary>
    /// <param name="outputDir">Output directory path</param>
    /// <param name="jobId">Batch job identifier</param>
    /// <param name="filename">Specific filename to download</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Path to the downloaded file</returns>
    Task<string> BatchDownloadAsync(
        string outputDir,
        string jobId,
        string filename,
        CancellationToken cancellationToken = default);

    // ========================================================================
    // Symbology API Methods
    // ========================================================================

    /// <summary>
    /// Resolve symbols from one symbology type to another
    /// </summary>
    /// <param name="dataset">Dataset name</param>
    /// <param name="symbols">List of symbols to resolve</param>
    /// <param name="stypeIn">Input symbology type</param>
    /// <param name="stypeOut">Output symbology type</param>
    /// <param name="startDate">Start date (inclusive)</param>
    /// <param name="endDate">End date (exclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Symbology resolution with mappings, partial matches, and not found symbols</returns>
    Task<SymbologyResolution> SymbologyResolveAsync(
        string dataset,
        IEnumerable<string> symbols,
        SType stypeIn,
        SType stypeOut,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);
}
