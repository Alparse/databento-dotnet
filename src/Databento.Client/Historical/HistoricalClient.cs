using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Channels;
using Databento.Client.Metadata;
using Databento.Client.Models;
using Databento.Client.Models.Batch;
using Databento.Client.Models.Metadata;
using Databento.Client.Models.Symbology;
using Databento.Interop;
using Databento.Interop.Handles;
using Databento.Interop.Native;

namespace Databento.Client.Historical;

/// <summary>
/// Historical data client implementation
/// </summary>
public sealed class HistoricalClient : IHistoricalClient
{
    private readonly HistoricalClientHandle _handle;
    private readonly HistoricalGateway _gateway;
    private readonly string? _customHost;
    private readonly ushort? _customPort;
    private readonly VersionUpgradePolicy _upgradePolicy;
    private readonly string? _userAgent;
    private readonly TimeSpan _timeout;
    private bool _disposed;

    // CRITICAL FIX: Store active callbacks to prevent GC collection
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, RecordCallbackDelegate> _activeCallbacks = new();

    internal HistoricalClient(string apiKey)
        : this(apiKey, HistoricalGateway.Bo1, null, null, VersionUpgradePolicy.Upgrade, null, TimeSpan.FromSeconds(30))
    {
    }

    internal HistoricalClient(
        string apiKey,
        HistoricalGateway gateway,
        string? customHost,
        ushort? customPort,
        VersionUpgradePolicy upgradePolicy,
        string? userAgent,
        TimeSpan timeout)
    {
        if (string.IsNullOrEmpty(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

        _gateway = gateway;
        _customHost = customHost;
        _customPort = customPort;
        _upgradePolicy = upgradePolicy;
        _userAgent = userAgent;
        _timeout = timeout;

        byte[] errorBuffer = new byte[512];
        var handlePtr = NativeMethods.dbento_historical_create(apiKey, errorBuffer, (nuint)errorBuffer.Length);

        if (handlePtr == IntPtr.Zero)
        {
            var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
            throw new DbentoException($"Failed to create historical client: {error}");
        }

        _handle = new HistoricalClientHandle(handlePtr);

        // Note: Gateway, upgrade policy, and other settings are stored for future use
        // when native layer supports configuration. For now, defaults are used.
    }

    /// <summary>
    /// Query historical data for a time range
    /// </summary>
    public async IAsyncEnumerable<Record> GetRangeAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var channel = Channel.CreateUnbounded<Record>();
        var symbolArray = symbols.ToArray();

        // Convert times to nanoseconds since epoch
        long startTimeNs = startTime.ToUnixTimeMilliseconds() * 1_000_000;
        long endTimeNs = endTime.ToUnixTimeMilliseconds() * 1_000_000;

        // CRITICAL FIX: Create callback and store it to prevent GC
        var callbackId = Guid.NewGuid();
        RecordCallbackDelegate recordCallback;
        unsafe
        {
            recordCallback = (recordBytes, recordLength, recordType, userData) =>
            {
                try
                {
                    // CRITICAL FIX: Validate pointer before dereferencing
                    if (recordBytes == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Error: Received null pointer from native code");
                        return;
                    }

                    // CRITICAL FIX: Validate length to prevent integer overflow
                    if (recordLength == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("Error: Received zero-length record");
                        return;
                    }

                    if (recordLength > int.MaxValue)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error: Record too large: {recordLength} bytes exceeds maximum {int.MaxValue}");
                        return;
                    }

                    // Sanity check: reasonable maximum record size (10MB)
                    const int MaxReasonableRecordSize = 10 * 1024 * 1024;
                    if (recordLength > MaxReasonableRecordSize)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error: Record suspiciously large: {recordLength} bytes");
                        return;
                    }

                    var bytes = new byte[recordLength];
                    unsafe
                    {
                        Marshal.Copy((IntPtr)recordBytes, bytes, 0, (int)recordLength);
                    }

                    var record = Record.FromBytes(bytes, recordType);
                    channel.Writer.TryWrite(record);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing record: {ex.Message}");
                }
            };
        }

        // Store callback to prevent GC collection while native code holds reference
        _activeCallbacks[callbackId] = recordCallback;

        // Start query on background thread
        var queryTask = Task.Run(() =>
        {
            try
            {
                byte[] errorBuffer = new byte[512];

                var result = NativeMethods.dbento_historical_get_range(
                    _handle,
                    dataset,
                    schema.ToSchemaString(),
                    symbolArray,
                    (nuint)symbolArray.Length,
                    startTimeNs,
                    endTimeNs,
                    recordCallback,
                    IntPtr.Zero,
                    errorBuffer,
                    (nuint)errorBuffer.Length);

                if (result != 0)
                {
                    var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                    throw new DbentoException($"Historical query failed: {error}", result);
                }
            }
            finally
            {
                channel.Writer.Complete();
                // Remove callback from active set after native operation completes
                _activeCallbacks.TryRemove(callbackId, out _);
            }
        }, cancellationToken);

        // Stream results
        await foreach (var record in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return record;
        }

        await queryTask;
    }

    /// <summary>
    /// Query historical data and save directly to a DBN file
    /// </summary>
    public async Task<string> GetRangeToFileAsync(
        string filePath,
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        var symbolArray = symbols.ToArray();

        // Convert times to nanoseconds since epoch
        long startTimeNs = startTime.ToUnixTimeMilliseconds() * 1_000_000;
        long endTimeNs = endTime.ToUnixTimeMilliseconds() * 1_000_000;

        return await Task.Run(() =>
        {
            byte[] errorBuffer = new byte[512];

            var result = NativeMethods.dbento_historical_get_range_to_file(
                _handle,
                filePath,
                dataset,
                schema.ToSchemaString(),
                symbolArray,
                (nuint)symbolArray.Length,
                startTimeNs,
                endTimeNs,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (result != 0)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to save historical data to file: {error}", result);
            }

            return filePath;
        }, cancellationToken);
    }

    /// <summary>
    /// Get metadata for a historical query
    /// Note: This feature is currently not fully implemented in the native layer
    /// </summary>
    public IMetadata? GetMetadata(
        string dataset,
        Schema schema,
        DateTimeOffset startTime,
        DateTimeOffset endTime)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Convert times to nanoseconds since epoch
        long startTimeNs = startTime.ToUnixTimeMilliseconds() * 1_000_000;
        long endTimeNs = endTime.ToUnixTimeMilliseconds() * 1_000_000;

        byte[] errorBuffer = new byte[512];

        var metadataHandle = NativeMethods.dbento_historical_get_metadata(
            _handle,
            dataset,
            schema.ToSchemaString(),
            startTimeNs,
            endTimeNs,
            errorBuffer,
            (nuint)errorBuffer.Length);

        if (metadataHandle == IntPtr.Zero)
        {
            // Native layer doesn't support metadata-only queries yet
            return null;
        }

        return new Metadata.Metadata(new MetadataHandle(metadataHandle));
    }

    // ========================================================================
    // Metadata API Methods
    // ========================================================================

    /// <summary>
    /// List all publishers
    /// </summary>
    public async Task<IReadOnlyList<PublisherDetail>> ListPublishersAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await Task.Run(() =>
        {
            byte[] errorBuffer = new byte[512];
            var jsonPtr = NativeMethods.dbento_metadata_list_publishers(
                _handle, errorBuffer, (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to list publishers: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "[]";
                var publishers = JsonSerializer.Deserialize<List<PublisherDetail>>(json) ?? new List<PublisherDetail>();
                return (IReadOnlyList<PublisherDetail>)publishers;
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// List datasets, optionally filtered by venue
    /// </summary>
    public async Task<IReadOnlyList<string>> ListDatasetsAsync(
        string? venue = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await Task.Run(() =>
        {
            byte[] errorBuffer = new byte[512];
            var jsonPtr = NativeMethods.dbento_metadata_list_datasets(
                _handle, venue, errorBuffer, (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to list datasets: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "[]";
                var datasets = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                return (IReadOnlyList<string>)datasets;
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// List schemas available for a dataset
    /// </summary>
    public async Task<IReadOnlyList<Schema>> ListSchemasAsync(
        string dataset,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await Task.Run(() =>
        {
            byte[] errorBuffer = new byte[512];
            var jsonPtr = NativeMethods.dbento_metadata_list_schemas(
                _handle, dataset, errorBuffer, (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to list schemas: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "[]";
                var schemaStrings = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                var schemas = schemaStrings.Select(s => SchemaExtensions.ParseSchema(s)).ToList();
                return (IReadOnlyList<Schema>)schemas;
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// List fields for a given encoding and schema
    /// </summary>
    public async Task<IReadOnlyList<FieldDetail>> ListFieldsAsync(
        Encoding encoding,
        Schema schema,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await Task.Run(() =>
        {
            byte[] errorBuffer = new byte[512];
            var jsonPtr = NativeMethods.dbento_metadata_list_fields(
                _handle,
                encoding.ToEncodingString(),
                schema.ToSchemaString(),
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to list fields: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "[]";
                var fields = JsonSerializer.Deserialize<List<FieldDetail>>(json) ?? new List<FieldDetail>();
                return (IReadOnlyList<FieldDetail>)fields;
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Get dataset availability condition
    /// </summary>
    public async Task<DatasetConditionInfo> GetDatasetConditionAsync(
        string dataset,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await Task.Run(() =>
        {
            byte[] errorBuffer = new byte[512];
            var jsonPtr = NativeMethods.dbento_metadata_get_dataset_condition(
                _handle, dataset, errorBuffer, (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to get dataset condition: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "{}";
                return JsonSerializer.Deserialize<DatasetConditionInfo>(json)
                    ?? throw new DbentoException("Failed to deserialize dataset condition");
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Get dataset time range
    /// </summary>
    public async Task<DatasetRange> GetDatasetRangeAsync(
        string dataset,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await Task.Run(() =>
        {
            byte[] errorBuffer = new byte[512];
            var jsonPtr = NativeMethods.dbento_metadata_get_dataset_range(
                _handle, dataset, errorBuffer, (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to get dataset range: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "{}";
                return JsonSerializer.Deserialize<DatasetRange>(json)
                    ?? throw new DbentoException("Failed to deserialize dataset range");
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Get record count for a query
    /// </summary>
    public async Task<ulong> GetRecordCountAsync(
        string dataset,
        Schema schema,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var symbolArray = symbols.ToArray();
        long startTimeNs = startTime.ToUnixTimeMilliseconds() * 1_000_000;
        long endTimeNs = endTime.ToUnixTimeMilliseconds() * 1_000_000;

        return await Task.Run(() =>
        {
            byte[] errorBuffer = new byte[512];
            var count = NativeMethods.dbento_metadata_get_record_count(
                _handle,
                dataset,
                schema.ToSchemaString(),
                startTimeNs,
                endTimeNs,
                symbolArray,
                (nuint)symbolArray.Length,
                errorBuffer,
                (nuint)errorBuffer.Length);

            // If count is ulong.MaxValue, an error occurred
            if (count == ulong.MaxValue)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to get record count: {error}");
            }

            return count;
        }, cancellationToken);
    }

    /// <summary>
    /// Get billable size for a query
    /// </summary>
    public async Task<ulong> GetBillableSizeAsync(
        string dataset,
        Schema schema,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var symbolArray = symbols.ToArray();
        long startTimeNs = startTime.ToUnixTimeMilliseconds() * 1_000_000;
        long endTimeNs = endTime.ToUnixTimeMilliseconds() * 1_000_000;

        return await Task.Run(() =>
        {
            byte[] errorBuffer = new byte[512];
            var size = NativeMethods.dbento_metadata_get_billable_size(
                _handle,
                dataset,
                schema.ToSchemaString(),
                startTimeNs,
                endTimeNs,
                symbolArray,
                (nuint)symbolArray.Length,
                errorBuffer,
                (nuint)errorBuffer.Length);

            // If size is ulong.MaxValue, an error occurred
            if (size == ulong.MaxValue)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to get billable size: {error}");
            }

            return size;
        }, cancellationToken);
    }

    /// <summary>
    /// Get cost estimate for a query
    /// </summary>
    public async Task<decimal> GetCostAsync(
        string dataset,
        Schema schema,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var symbolArray = symbols.ToArray();
        long startTimeNs = startTime.ToUnixTimeMilliseconds() * 1_000_000;
        long endTimeNs = endTime.ToUnixTimeMilliseconds() * 1_000_000;

        return await Task.Run(() =>
        {
            byte[] errorBuffer = new byte[512];
            var costPtr = NativeMethods.dbento_metadata_get_cost(
                _handle,
                dataset,
                schema.ToSchemaString(),
                startTimeNs,
                endTimeNs,
                symbolArray,
                (nuint)symbolArray.Length,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (costPtr == IntPtr.Zero)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to get cost: {error}");
            }

            try
            {
                var costString = Marshal.PtrToStringUTF8(costPtr) ?? "0";
                return decimal.Parse(costString);
            }
            finally
            {
                NativeMethods.dbento_free_string(costPtr);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Get combined billing information for a query
    /// </summary>
    public async Task<BillingInfo> GetBillingInfoAsync(
        string dataset,
        Schema schema,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var symbolArray = symbols.ToArray();
        long startTimeNs = startTime.ToUnixTimeMilliseconds() * 1_000_000;
        long endTimeNs = endTime.ToUnixTimeMilliseconds() * 1_000_000;

        return await Task.Run(() =>
        {
            byte[] errorBuffer = new byte[512];
            var jsonPtr = NativeMethods.dbento_metadata_get_billing_info(
                _handle,
                dataset,
                schema.ToSchemaString(),
                startTimeNs,
                endTimeNs,
                symbolArray,
                (nuint)symbolArray.Length,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to get billing info: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "{}";
                return JsonSerializer.Deserialize<BillingInfo>(json)
                    ?? throw new DbentoException("Failed to deserialize billing info");
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Get unit prices per schema for all feed modes
    /// </summary>
    /// <param name="dataset">Dataset name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of unit prices for each feed mode</returns>
    public Task<IReadOnlyList<UnitPricesForMode>> ListUnitPricesAsync(
        string dataset,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);

        return Task.Run(() =>
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            byte[] errorBuffer = new byte[1024];

            var handlePtr = NativeMethods.dbento_historical_list_unit_prices(
                _handle,
                dataset,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (handlePtr == IntPtr.Zero)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to get unit prices: {error}");
            }

            using var unitPricesHandle = new UnitPricesHandle(handlePtr);

            var result = new List<UnitPricesForMode>();
            nuint modesCount = NativeMethods.dbento_unit_prices_get_modes_count(handlePtr);

            for (nuint i = 0; i < modesCount; i++)
            {
                int modeValue = NativeMethods.dbento_unit_prices_get_mode(handlePtr, i);
                if (modeValue < 0) continue;

                var mode = (PricingMode)modeValue;
                var unitPricesDict = new Dictionary<Schema, decimal>();

                nuint schemaCount = NativeMethods.dbento_unit_prices_get_schema_count(handlePtr, i);
                for (nuint j = 0; j < schemaCount; j++)
                {
                    int schemaValue;
                    double price;

                    int resultCode = NativeMethods.dbento_unit_prices_get_schema_price(
                        handlePtr, i, j, out schemaValue, out price);

                    if (resultCode == 0)
                    {
                        var schema = (Schema)schemaValue;
                        unitPricesDict[schema] = (decimal)price;
                    }
                }

                result.Add(new UnitPricesForMode
                {
                    Mode = mode,
                    UnitPrices = unitPricesDict
                });
            }

            return (IReadOnlyList<UnitPricesForMode>)result;
        }, cancellationToken);
    }

    // ========================================================================
    // Batch API Methods
    // ========================================================================

    /// <summary>
    /// Submit a new batch job for bulk historical data download.
    /// WARNING: This operation will incur a cost.
    /// </summary>
    public async Task<BatchJob> BatchSubmitJobAsync(
        string dataset,
        IEnumerable<string> symbols,
        Schema schema,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var symbolArray = symbols.ToArray();
        long startTimeNs = startTime.ToUnixTimeMilliseconds() * 1_000_000;
        long endTimeNs = endTime.ToUnixTimeMilliseconds() * 1_000_000;

        return await Task.Run(() =>
        {
            byte[] errorBuffer = new byte[512];
            var jsonPtr = NativeMethods.dbento_batch_submit_job(
                _handle,
                dataset,
                schema.ToSchemaString(),
                symbolArray,
                (nuint)symbolArray.Length,
                startTimeNs,
                endTimeNs,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to submit batch job: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "{}";
                return JsonSerializer.Deserialize<BatchJob>(json)
                    ?? throw new DbentoException("Failed to deserialize batch job");
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Submit a new batch job with advanced options for bulk historical data download.
    /// WARNING: This operation will incur a cost.
    /// </summary>
    public async Task<BatchJob> BatchSubmitJobAsync(
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
        CancellationToken cancellationToken = default)
    {
        // For now, delegate to basic version with defaults
        // Advanced version would require additional native wrapper implementation
        return await BatchSubmitJobAsync(dataset, symbols, schema, startTime, endTime, cancellationToken);
    }

    /// <summary>
    /// List previous batch jobs
    /// </summary>
    public async Task<IReadOnlyList<BatchJob>> BatchListJobsAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await Task.Run(() =>
        {
            byte[] errorBuffer = new byte[512];
            var jsonPtr = NativeMethods.dbento_batch_list_jobs(
                _handle,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to list batch jobs: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "[]";
                var jobs = JsonSerializer.Deserialize<List<BatchJob>>(json) ?? new List<BatchJob>();
                return (IReadOnlyList<BatchJob>)jobs;
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// List previous batch jobs filtered by state and date
    /// </summary>
    public async Task<IReadOnlyList<BatchJob>> BatchListJobsAsync(
        IEnumerable<JobState> states,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        // For now, get all jobs and filter client-side
        // Native layer would need additional implementation for server-side filtering
        var allJobs = await BatchListJobsAsync(cancellationToken);
        var stateSet = new HashSet<JobState>(states);

        return allJobs
            .Where(job => stateSet.Contains(job.State))
            .Where(job => DateTimeOffset.Parse(job.TsReceived) >= since)
            .ToList();
    }

    /// <summary>
    /// List all files associated with a batch job
    /// </summary>
    public async Task<IReadOnlyList<BatchFileDesc>> BatchListFilesAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await Task.Run(() =>
        {
            byte[] errorBuffer = new byte[512];
            var jsonPtr = NativeMethods.dbento_batch_list_files(
                _handle,
                jobId,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to list batch files: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "[]";
                var files = JsonSerializer.Deserialize<List<BatchFileDesc>>(json) ?? new List<BatchFileDesc>();
                return (IReadOnlyList<BatchFileDesc>)files;
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Download all files from a batch job to a directory
    /// </summary>
    public async Task<IReadOnlyList<string>> BatchDownloadAsync(
        string outputDir,
        string jobId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await Task.Run(() =>
        {
            byte[] errorBuffer = new byte[512];
            var jsonPtr = NativeMethods.dbento_batch_download_all(
                _handle,
                outputDir,
                jobId,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to download batch files: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "[]";
                var paths = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                return (IReadOnlyList<string>)paths;
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Download a specific file from a batch job
    /// </summary>
    public async Task<string> BatchDownloadAsync(
        string outputDir,
        string jobId,
        string filename,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        return await Task.Run(() =>
        {
            byte[] errorBuffer = new byte[512];
            var pathPtr = NativeMethods.dbento_batch_download_file(
                _handle,
                outputDir,
                jobId,
                filename,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (pathPtr == IntPtr.Zero)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to download batch file: {error}");
            }

            try
            {
                return Marshal.PtrToStringUTF8(pathPtr) ?? string.Empty;
            }
            finally
            {
                NativeMethods.dbento_free_string(pathPtr);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Resolve symbols from one symbology type to another over a date range
    /// </summary>
    /// <param name="dataset">Dataset name (e.g., "GLBX.MDP3")</param>
    /// <param name="symbols">Symbols to resolve</param>
    /// <param name="stypeIn">Input symbology type</param>
    /// <param name="stypeOut">Output symbology type</param>
    /// <param name="startDate">Start date (inclusive)</param>
    /// <param name="endDate">End date (exclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Symbology resolution result</returns>
    public Task<SymbologyResolution> SymbologyResolveAsync(
        string dataset,
        IEnumerable<string> symbols,
        SType stypeIn,
        SType stypeOut,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        ArgumentNullException.ThrowIfNull(symbols);

        return Task.Run(() =>
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var symbolArray = symbols.ToArray();
            if (symbolArray.Length == 0)
            {
                throw new ArgumentException("Symbols collection cannot be empty", nameof(symbols));
            }

            byte[] errorBuffer = new byte[1024];

            // Convert SType enums to strings (lowercase with underscores)
            string stypeInStr = ConvertStypeToString(stypeIn);
            string stypeOutStr = ConvertStypeToString(stypeOut);

            // Format dates as YYYY-MM-DD
            string startDateStr = startDate.ToString("yyyy-MM-dd");
            string endDateStr = endDate.ToString("yyyy-MM-dd");

            var handlePtr = NativeMethods.dbento_historical_symbology_resolve(
                _handle,
                dataset,
                symbolArray,
                (nuint)symbolArray.Length,
                stypeInStr,
                stypeOutStr,
                startDateStr,
                endDateStr,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (handlePtr == IntPtr.Zero)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Failed to resolve symbology: {error}");
            }

            using var resHandle = new SymbologyResolutionHandle(handlePtr);

            // Extract data from the native handle
            var mappings = new Dictionary<string, IReadOnlyList<MappingInterval>>();
            nuint mappingsCount = NativeMethods.dbento_symbology_resolution_mappings_count(handlePtr);

            for (nuint i = 0; i < mappingsCount; i++)
            {
                byte[] keyBuffer = new byte[256];
                int result = NativeMethods.dbento_symbology_resolution_get_mapping_key(
                    handlePtr, i, keyBuffer, (nuint)keyBuffer.Length);

                if (result != 0) continue;

                string key = System.Text.Encoding.UTF8.GetString(keyBuffer).TrimEnd('\0');

                // Get intervals for this key
                nuint intervalCount = NativeMethods.dbento_symbology_resolution_get_intervals_count(
                    handlePtr, key);

                var intervals = new List<MappingInterval>();
                for (nuint j = 0; j < intervalCount; j++)
                {
                    byte[] startDateBuffer = new byte[32];
                    byte[] endDateBuffer = new byte[32];
                    byte[] symbolBuffer = new byte[256];

                    result = NativeMethods.dbento_symbology_resolution_get_interval(
                        handlePtr, key, j,
                        startDateBuffer, (nuint)startDateBuffer.Length,
                        endDateBuffer, (nuint)endDateBuffer.Length,
                        symbolBuffer, (nuint)symbolBuffer.Length);

                    if (result == 0)
                    {
                        string startDateStrInterval = System.Text.Encoding.UTF8.GetString(startDateBuffer).TrimEnd('\0');
                        string endDateStrInterval = System.Text.Encoding.UTF8.GetString(endDateBuffer).TrimEnd('\0');
                        string symbol = System.Text.Encoding.UTF8.GetString(symbolBuffer).TrimEnd('\0');

                        intervals.Add(new MappingInterval
                        {
                            StartDate = DateOnly.ParseExact(startDateStrInterval, "yyyy-MM-dd"),
                            EndDate = DateOnly.ParseExact(endDateStrInterval, "yyyy-MM-dd"),
                            Symbol = symbol
                        });
                    }
                }

                mappings[key] = intervals;
            }

            // Get partial symbols
            var partial = new List<string>();
            nuint partialCount = NativeMethods.dbento_symbology_resolution_partial_count(handlePtr);
            for (nuint i = 0; i < partialCount; i++)
            {
                byte[] symbolBuffer = new byte[256];
                if (NativeMethods.dbento_symbology_resolution_get_partial(
                    handlePtr, i, symbolBuffer, (nuint)symbolBuffer.Length) == 0)
                {
                    partial.Add(System.Text.Encoding.UTF8.GetString(symbolBuffer).TrimEnd('\0'));
                }
            }

            // Get not found symbols
            var notFound = new List<string>();
            nuint notFoundCount = NativeMethods.dbento_symbology_resolution_not_found_count(handlePtr);
            for (nuint i = 0; i < notFoundCount; i++)
            {
                byte[] symbolBuffer = new byte[256];
                if (NativeMethods.dbento_symbology_resolution_get_not_found(
                    handlePtr, i, symbolBuffer, (nuint)symbolBuffer.Length) == 0)
                {
                    notFound.Add(System.Text.Encoding.UTF8.GetString(symbolBuffer).TrimEnd('\0'));
                }
            }

            return new SymbologyResolution
            {
                Mappings = mappings,
                Partial = partial,
                NotFound = notFound,
                StypeIn = stypeIn,
                StypeOut = stypeOut
            };
        }, cancellationToken);
    }

    private static string ConvertStypeToString(SType stype)
    {
        return stype switch
        {
            SType.InstrumentId => "instrument_id",
            SType.RawSymbol => "raw_symbol",
            SType.Smart => "smart",
            SType.Continuous => "continuous",
            SType.Parent => "parent",
            SType.NasdaqSymbol => "nasdaq_symbol",
            SType.CmsSymbol => "cms_symbol",
            SType.Isin => "isin",
            SType.UsCode => "us_code",
            SType.BbgCompId => "bbg_comp_id",
            SType.BbgCompTicker => "bbg_comp_ticker",
            SType.Figi => "figi",
            SType.FigiTicker => "figi_ticker",
            _ => throw new ArgumentException($"Unknown SType: {stype}", nameof(stype))
        };
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;

        _disposed = true;
        _handle?.Dispose();

        return ValueTask.CompletedTask;
    }
}
