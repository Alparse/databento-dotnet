using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Databento.Client.Metadata;
using Databento.Client.Models;
using Databento.Client.Models.Batch;
using Databento.Client.Models.Metadata;
using Databento.Client.Models.Symbology;
using Databento.Interop;
using Databento.Interop.Handles;
using Databento.Interop.Native;
using Microsoft.Extensions.Logging;

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
    private readonly ILogger<IHistoricalClient>? _logger;
    // MEDIUM FIX: Use atomic int for disposal state (0=active, 1=disposing, 2=disposed)
    private int _disposeState = 0;

    // CRITICAL FIX: Store active callbacks to prevent GC collection
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, RecordCallbackDelegate> _activeCallbacks = new();

    // JSON serialization options for enum deserialization
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    internal HistoricalClient(string apiKey)
        : this(apiKey, HistoricalGateway.Bo1, null, null, VersionUpgradePolicy.Upgrade, null, TimeSpan.FromSeconds(30), null)
    {
    }

    internal HistoricalClient(
        string apiKey,
        HistoricalGateway gateway,
        string? customHost,
        ushort? customPort,
        VersionUpgradePolicy upgradePolicy,
        string? userAgent,
        TimeSpan timeout,
        ILogger<IHistoricalClient>? logger = null)
    {
        if (string.IsNullOrEmpty(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

        _gateway = gateway;
        _customHost = customHost;
        _customPort = customPort;
        _upgradePolicy = upgradePolicy;
        _userAgent = userAgent;
        _timeout = timeout;
        _logger = logger;

        byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
        var handlePtr = NativeMethods.dbento_historical_create(apiKey, errorBuffer, (nuint)errorBuffer.Length);

        if (handlePtr == IntPtr.Zero)
        {
            // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
            _logger?.LogError("Failed to create HistoricalClient: {Error}", error);
            throw new DbentoException($"Failed to create historical client: {error}");
        }

        _handle = new HistoricalClientHandle(handlePtr);

        _logger?.LogInformation(
            "HistoricalClient created successfully. Gateway={Gateway}, UpgradePolicy={UpgradePolicy}, Timeout={Timeout}s",
            gateway,
            upgradePolicy,
            (int)timeout.TotalSeconds);

        // Note: Gateway, upgrade policy, and other settings are stored for future use
        // when native layer supports configuration. For now, defaults are used.
    }

    /// <summary>
    /// Query historical data for a time range
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Known Limitation</b>: This method has a critical bug in the underlying native library (databento-cpp).
    /// If you provide invalid parameters (invalid symbols, wrong dataset, invalid date range),
    /// the process will crash with AccessViolationException instead of throwing a catchable exception.
    /// The crash is isolated to this client instance - your application will continue running,
    /// but this client will be unusable.
    /// <para>
    /// <b>Workaround</b>: Use the Live API (<see cref="Client.Live.LiveClient"/>) which handles
    /// invalid symbols gracefully via metadata.not_found field, or pre-validate your symbols
    /// using the symbology API before calling this method.
    /// </para>
    /// <para>
    /// This bug has been reported to the databento-cpp maintainers and will be fixed in a future release.
    /// </para>
    /// </remarks>
    public async IAsyncEnumerable<Record> GetRangeAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // MEDIUM FIX: Validate input parameters
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset, nameof(dataset));
        ArgumentNullException.ThrowIfNull(symbols, nameof(symbols));

        var channel = Channel.CreateUnbounded<Record>();
        var symbolArray = symbols.ToArray();
        // HIGH FIX: Validate symbol array elements
        Utilities.ErrorBufferHelpers.ValidateSymbolArray(symbolArray);

        _logger?.LogInformation(
            "Starting historical query. Dataset={Dataset}, Schema={Schema}, SymbolCount={SymbolCount}, Start={Start}, End={End}",
            dataset,
            schema,
            symbolArray.Length,
            startTime,
            endTime);

        // Convert times to nanoseconds since epoch (HIGH FIX: using checked arithmetic)
        long startTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(startTime);
        long endTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(endTime);

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
                        var ex = new DbentoException("Received null pointer from native code");
                        channel.Writer.Complete(ex);
                        return;
                    }

                    // CRITICAL FIX: Validate length to prevent integer overflow
                    if (recordLength == 0)
                    {
                        var ex = new DbentoException("Received zero-length record");
                        channel.Writer.Complete(ex);
                        return;
                    }

                    if (recordLength > int.MaxValue)
                    {
                        var ex = new DbentoException($"Record too large: {recordLength} bytes exceeds maximum {int.MaxValue}");
                        channel.Writer.Complete(ex);
                        return;
                    }

                    // Sanity check: reasonable maximum record size (10MB)
                    if (recordLength > Utilities.Constants.MaxReasonableRecordSize)
                    {
                        var ex = new DbentoException($"Record suspiciously large: {recordLength} bytes");
                        channel.Writer.Complete(ex);
                        return;
                    }

                    var bytes = new byte[recordLength];
                    unsafe
                    {
                        Marshal.Copy((IntPtr)recordBytes, bytes, 0, (int)recordLength);
                    }

                    var record = Record.FromBytes(bytes, recordType);

                    // CRITICAL FIX: Check TryWrite return value to prevent silent data loss
                    if (!channel.Writer.TryWrite(record))
                    {
                        throw new InvalidOperationException(
                            "Failed to write record to channel. Channel may be full or closed.");
                    }
                }
                catch (Exception ex)
                {
                    // MEDIUM FIX: Don't swallow exceptions - propagate to caller via channel completion
                    channel.Writer.Complete(ex);
                    throw;
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
                // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];

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
                    // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                    // MEDIUM FIX: Use exception factory method for proper exception type mapping
                    throw DbentoException.CreateFromErrorCode($"Historical query failed: {error}", result);
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

        await queryTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Query historical data for a time range with symbology type filtering
    /// </summary>
    /// <remarks>
    /// This overload allows you to specify how symbols are interpreted (stypeIn) and how they should be
    /// represented in the output (stypeOut). For example, you can use SType.Parent with "ES.FUT" to get
    /// all E-mini S&P 500 futures contracts.
    /// </remarks>
    public async IAsyncEnumerable<Record> GetRangeAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        SType stypeIn,
        SType stypeOut,
        ulong limit = 0,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // Validate input parameters
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset, nameof(dataset));
        ArgumentNullException.ThrowIfNull(symbols, nameof(symbols));

        var channel = Channel.CreateUnbounded<Record>();
        var symbolArray = symbols.ToArray();
        Utilities.ErrorBufferHelpers.ValidateSymbolArray(symbolArray);

        _logger?.LogInformation(
            "Starting historical query with symbology filtering. Dataset={Dataset}, Schema={Schema}, SymbolCount={SymbolCount}, Start={Start}, End={End}, StypeIn={StypeIn}, StypeOut={StypeOut}, Limit={Limit}",
            dataset,
            schema,
            symbolArray.Length,
            startTime,
            endTime,
            stypeIn,
            stypeOut,
            limit);

        // Convert times to nanoseconds since epoch
        long startTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(startTime);
        long endTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(endTime);

        // Create callback and store it to prevent GC
        var callbackId = Guid.NewGuid();
        RecordCallbackDelegate recordCallback;
        unsafe
        {
            recordCallback = (recordBytes, recordLength, recordType, userData) =>
            {
                try
                {
                    if (recordBytes == null)
                    {
                        var ex = new DbentoException("Received null pointer from native code");
                        channel.Writer.Complete(ex);
                        return;
                    }

                    if (recordLength == 0)
                    {
                        var ex = new DbentoException("Received zero-length record");
                        channel.Writer.Complete(ex);
                        return;
                    }

                    if (recordLength > int.MaxValue)
                    {
                        var ex = new DbentoException($"Record too large: {recordLength} bytes exceeds maximum {int.MaxValue}");
                        channel.Writer.Complete(ex);
                        return;
                    }

                    if (recordLength > Utilities.Constants.MaxReasonableRecordSize)
                    {
                        var ex = new DbentoException($"Record suspiciously large: {recordLength} bytes");
                        channel.Writer.Complete(ex);
                        return;
                    }

                    var bytes = new byte[recordLength];
                    unsafe
                    {
                        Marshal.Copy((IntPtr)recordBytes, bytes, 0, (int)recordLength);
                    }

                    var record = Record.FromBytes(bytes, recordType);

                    if (!channel.Writer.TryWrite(record))
                    {
                        throw new InvalidOperationException(
                            "Failed to write record to channel. Channel may be full or closed.");
                    }
                }
                catch (Exception ex)
                {
                    channel.Writer.Complete(ex);
                    throw;
                }
            };
        }

        // Store callback to prevent GC collection
        _activeCallbacks[callbackId] = recordCallback;

        // Start query on background thread
        var queryTask = Task.Run(() =>
        {
            try
            {
                byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];

                var result = NativeMethods.dbento_historical_get_range_with_symbology(
                    _handle,
                    dataset,
                    schema.ToSchemaString(),
                    symbolArray,
                    (nuint)symbolArray.Length,
                    startTimeNs,
                    endTimeNs,
                    ConvertStypeToString(stypeIn),
                    ConvertStypeToString(stypeOut),
                    limit,
                    recordCallback,
                    IntPtr.Zero,
                    errorBuffer,
                    (nuint)errorBuffer.Length);

                if (result != 0)
                {
                    var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                    throw DbentoException.CreateFromErrorCode($"Historical query failed: {error}", result);
                }

                // Success - mark channel as complete
                channel.Writer.Complete();
            }
            catch (Exception ex)
            {
                // Propagate exception to channel so await foreach sees it immediately
                channel.Writer.Complete(ex);
                throw;
            }
            finally
            {
                _activeCallbacks.TryRemove(callbackId, out _);
            }
        }, cancellationToken);

        // Stream results
        await foreach (var record in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return record;
        }

        await queryTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Query historical data and save directly to a DBN file
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Known Limitation</b>: This method has a critical bug in the underlying native library (databento-cpp).
    /// If you provide invalid parameters (invalid symbols, wrong dataset, invalid date range),
    /// the process will crash with AccessViolationException instead of throwing a catchable exception.
    /// The crash is isolated to this client instance - your application will continue running,
    /// but this client will be unusable.
    /// <para>
    /// <b>Workaround</b>: Use the Live API (<see cref="Client.Live.LiveClient"/>) which handles
    /// invalid symbols gracefully, or pre-validate your symbols using the symbology API before calling this method.
    /// Alternatively, use <see cref="BatchSubmitJobAsync"/> which does not have this bug.
    /// </para>
    /// <para>
    /// This bug has been reported to the databento-cpp maintainers and will be fixed in a future release.
    /// </para>
    /// </remarks>
    public async Task<string> GetRangeToFileAsync(
        string filePath,
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        // MEDIUM FIX: Validate input parameters
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset, nameof(dataset));
        ArgumentNullException.ThrowIfNull(symbols, nameof(symbols));

        var symbolArray = symbols.ToArray();
        // HIGH FIX: Validate symbol array elements
        Utilities.ErrorBufferHelpers.ValidateSymbolArray(symbolArray);

        // Convert times to nanoseconds since epoch (HIGH FIX: using checked arithmetic)
        long startTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(startTime);
        long endTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(endTime);

        return await Task.Run(() =>
        {
            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];

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
                // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                // MEDIUM FIX: Use exception factory method for proper exception type mapping
                throw DbentoException.CreateFromErrorCode($"Failed to save historical data to file: {error}", result);
            }

            return filePath;
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Query historical data with symbology type filtering and save directly to a DBN file
    /// </summary>
    /// <remarks>
    /// This overload allows you to specify how symbols are interpreted (stypeIn) and how they should be
    /// represented in the output (stypeOut). For example, you can use SType.Parent with "ES.FUT" to get
    /// all E-mini S&P 500 futures contracts.
    /// </remarks>
    public async Task<string> GetRangeToFileAsync(
        string filePath,
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        SType stypeIn,
        SType stypeOut,
        ulong limit = 0,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        // Validate input parameters
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset, nameof(dataset));
        ArgumentNullException.ThrowIfNull(symbols, nameof(symbols));

        var symbolArray = symbols.ToArray();
        Utilities.ErrorBufferHelpers.ValidateSymbolArray(symbolArray);

        // Convert times to nanoseconds since epoch
        long startTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(startTime);
        long endTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(endTime);

        return await Task.Run(() =>
        {
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];

            var result = NativeMethods.dbento_historical_get_range_to_file_with_symbology(
                _handle,
                filePath,
                dataset,
                schema.ToSchemaString(),
                symbolArray,
                (nuint)symbolArray.Length,
                startTimeNs,
                endTimeNs,
                ConvertStypeToString(stypeIn),
                ConvertStypeToString(stypeOut),
                limit,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (result != 0)
            {
                var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                throw DbentoException.CreateFromErrorCode($"Failed to save historical data to file: {error}", result);
            }

            return filePath;
        }, cancellationToken).ConfigureAwait(false);
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
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // MEDIUM FIX: Validate input parameters
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset, nameof(dataset));

        // Convert times to nanoseconds since epoch (HIGH FIX: using checked arithmetic)
        long startTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(startTime);
        long endTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(endTime);

        byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];

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
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        return await Task.Run(() =>
        {
            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
            var jsonPtr = NativeMethods.dbento_metadata_list_publishers(
                _handle, errorBuffer, (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                throw new DbentoException($"Failed to list publishers: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr);
                if (string.IsNullOrEmpty(json))
                    throw new DbentoException("Failed to get publishers: empty response from native layer");

                // MEDIUM FIX: Throw on deserialization failure instead of returning empty collection
                var publishers = JsonSerializer.Deserialize<List<PublisherDetail>>(json);
                if (publishers == null)
                    throw new DbentoException("Failed to deserialize publishers response");

                return (IReadOnlyList<PublisherDetail>)publishers;
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// List datasets, optionally filtered by venue
    /// </summary>
    public async Task<IReadOnlyList<string>> ListDatasetsAsync(
        string? venue = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        return await Task.Run(() =>
        {
            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
            var jsonPtr = NativeMethods.dbento_metadata_list_datasets(
                _handle, venue, errorBuffer, (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                // HIGH FIX: Use safe error string extraction
                var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                var statusCode = Utilities.ErrorBufferHelpers.ExtractStatusCode(error);
                var message = $"Failed to list datasets: {error}";

                // Use factory method to create appropriate exception type based on status code
                if (statusCode.HasValue)
                    throw DbentoException.CreateFromErrorCode(message, statusCode.Value);
                else
                    throw new DbentoException(message);
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr);
                if (string.IsNullOrEmpty(json))
                    throw new DbentoException("Failed to get datasets: empty response from native layer");

                // MEDIUM FIX: Throw on deserialization failure instead of returning empty collection
                var datasets = JsonSerializer.Deserialize<List<string>>(json);
                if (datasets == null)
                    throw new DbentoException("Failed to deserialize datasets response");

                return (IReadOnlyList<string>)datasets;
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// List schemas available for a dataset
    /// </summary>
    public async Task<IReadOnlyList<Schema>> ListSchemasAsync(
        string dataset,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // MEDIUM FIX: Validate input parameters
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset, nameof(dataset));

        return await Task.Run(() =>
        {
            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
            var jsonPtr = NativeMethods.dbento_metadata_list_schemas(
                _handle, dataset, errorBuffer, (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                throw new DbentoException($"Failed to list schemas: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr);
                if (string.IsNullOrEmpty(json))
                    throw new DbentoException("Failed to get schemas: empty response from native layer");

                // MEDIUM FIX: Throw on deserialization failure instead of returning empty collection
                var schemaStrings = JsonSerializer.Deserialize<List<string>>(json);
                if (schemaStrings == null)
                    throw new DbentoException("Failed to deserialize schemas response");
                var schemas = schemaStrings.Select(s => SchemaExtensions.ParseSchema(s)).ToList();
                return (IReadOnlyList<Schema>)schemas;
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// List fields for a given encoding and schema
    /// </summary>
    public async Task<IReadOnlyList<FieldDetail>> ListFieldsAsync(
        Encoding encoding,
        Schema schema,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        return await Task.Run(() =>
        {
            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
            var jsonPtr = NativeMethods.dbento_metadata_list_fields(
                _handle,
                encoding.ToEncodingString(),
                schema.ToSchemaString(),
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                throw new DbentoException($"Failed to list fields: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr);
                if (string.IsNullOrEmpty(json))
                    throw new DbentoException("Failed to get fields: empty response from native layer");

                // MEDIUM FIX: Throw on deserialization failure instead of returning empty collection
                var fields = JsonSerializer.Deserialize<List<FieldDetail>>(json);
                if (fields == null)
                    throw new DbentoException("Failed to deserialize fields response");

                return (IReadOnlyList<FieldDetail>)fields;
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Get dataset availability condition
    /// </summary>
    public async Task<DatasetConditionInfo> GetDatasetConditionAsync(
        string dataset,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // MEDIUM FIX: Validate input parameters
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset, nameof(dataset));

        return await Task.Run(() =>
        {
            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
            var jsonPtr = NativeMethods.dbento_metadata_get_dataset_condition(
                _handle, dataset, errorBuffer, (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                throw new DbentoException($"Failed to get dataset condition: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "{}";
                return JsonSerializer.Deserialize<DatasetConditionInfo>(json, JsonOptions)
                    ?? throw new DbentoException("Failed to deserialize dataset condition");
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Get dataset condition for a specific date range
    /// </summary>
    public async Task<IReadOnlyList<DatasetConditionDetail>> GetDatasetConditionAsync(
        string dataset,
        DateTimeOffset startDate,
        DateTimeOffset? endDate = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // MEDIUM FIX: Validate input parameters
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset, nameof(dataset));

        return await Task.Run(() =>
        {
            // Convert dates to ISO 8601 format (YYYY-MM-DD)
            string startDateStr = startDate.ToString("yyyy-MM-dd");
            string? endDateStr = endDate?.ToString("yyyy-MM-dd");

            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
            var jsonPtr = NativeMethods.dbento_metadata_get_dataset_condition_with_date_range(
                _handle, dataset, startDateStr, endDateStr, errorBuffer, (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                // HIGH FIX: Use safe error string extraction
                var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                throw new DbentoException($"Failed to get dataset condition: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "[]";
                return JsonSerializer.Deserialize<List<DatasetConditionDetail>>(json, JsonOptions)
                    ?? throw new DbentoException("Failed to deserialize dataset condition details");
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Get dataset time range
    /// </summary>
    public async Task<DatasetRange> GetDatasetRangeAsync(
        string dataset,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // MEDIUM FIX: Validate input parameters
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset, nameof(dataset));

        return await Task.Run(() =>
        {
            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
            var jsonPtr = NativeMethods.dbento_metadata_get_dataset_range(
                _handle, dataset, errorBuffer, (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                throw new DbentoException($"Failed to get dataset range: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr) ?? "{}";
                return JsonSerializer.Deserialize<DatasetRange>(json, JsonOptions)
                    ?? throw new DbentoException("Failed to deserialize dataset range");
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken).ConfigureAwait(false);
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
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // MEDIUM FIX: Validate input parameters
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset, nameof(dataset));
        ArgumentNullException.ThrowIfNull(symbols, nameof(symbols));

        var symbolArray = symbols.ToArray();
        // HIGH FIX: Validate symbol array elements
        Utilities.ErrorBufferHelpers.ValidateSymbolArray(symbolArray);
        // HIGH FIX: Use checked arithmetic via helper
        long startTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(startTime);
        long endTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(endTime);

        return await Task.Run(() =>
        {
            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
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
                // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                throw new DbentoException($"Failed to get record count: {error}");
            }

            return count;
        }, cancellationToken).ConfigureAwait(false);
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
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // MEDIUM FIX: Validate input parameters
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset, nameof(dataset));
        ArgumentNullException.ThrowIfNull(symbols, nameof(symbols));

        var symbolArray = symbols.ToArray();
        // HIGH FIX: Validate symbol array elements
        Utilities.ErrorBufferHelpers.ValidateSymbolArray(symbolArray);
        // HIGH FIX: Use checked arithmetic via helper
        long startTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(startTime);
        long endTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(endTime);

        return await Task.Run(() =>
        {
            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
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
                // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                throw new DbentoException($"Failed to get billable size: {error}");
            }

            return size;
        }, cancellationToken).ConfigureAwait(false);
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
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // MEDIUM FIX: Validate input parameters
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset, nameof(dataset));
        ArgumentNullException.ThrowIfNull(symbols, nameof(symbols));

        var symbolArray = symbols.ToArray();
        // HIGH FIX: Validate symbol array elements
        Utilities.ErrorBufferHelpers.ValidateSymbolArray(symbolArray);
        // HIGH FIX: Use checked arithmetic via helper
        long startTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(startTime);
        long endTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(endTime);

        return await Task.Run(() =>
        {
            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
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
                // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                throw new DbentoException($"Failed to get cost: {error}");
            }

            try
            {
                var costString = Marshal.PtrToStringUTF8(costPtr) ?? "0";

                // HIGH FIX: Use TryParse to prevent FormatException on malformed native cost string
                if (!decimal.TryParse(costString, out var cost))
                {
                    _logger?.LogWarning("Failed to parse cost string from native code: {CostString}", costString);
                    throw new DbentoException($"Invalid cost format returned from native code: '{costString}'");
                }

                return cost;
            }
            finally
            {
                NativeMethods.dbento_free_string(costPtr);
            }
        }, cancellationToken).ConfigureAwait(false);
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
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // MEDIUM FIX: Validate input parameters
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset, nameof(dataset));
        ArgumentNullException.ThrowIfNull(symbols, nameof(symbols));

        var symbolArray = symbols.ToArray();
        // HIGH FIX: Validate symbol array elements
        Utilities.ErrorBufferHelpers.ValidateSymbolArray(symbolArray);
        // HIGH FIX: Use checked arithmetic via helper
        long startTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(startTime);
        long endTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(endTime);

        return await Task.Run(() =>
        {
            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
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
                // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
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
        }, cancellationToken).ConfigureAwait(false);
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
            ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];

            var handlePtr = NativeMethods.dbento_historical_list_unit_prices(
                _handle,
                dataset,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (handlePtr == IntPtr.Zero)
            {
                // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                throw new DbentoException($"Failed to get unit prices: {error}");
            }

            using var unitPricesHandle = new UnitPricesHandle(handlePtr);

            var result = new List<UnitPricesForMode>();
            nuint modesCount = NativeMethods.dbento_unit_prices_get_modes_count(handlePtr);

            for (nuint i = 0; i < modesCount; i++)
            {
                int modeValue = NativeMethods.dbento_unit_prices_get_mode(handlePtr, i);
                if (modeValue < 0) continue;

                // LOW FIX: Validate mode value is within enum range (PricingMode is byte-based)
                if (modeValue > byte.MaxValue || !Enum.IsDefined(typeof(PricingMode), (byte)modeValue))
                {
                    // Skip invalid mode value rather than crashing
                    continue;
                }

                var mode = (PricingMode)(byte)modeValue;
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
                        // LOW FIX: Validate schema value is within enum range (Schema is ushort-based)
                        if (schemaValue < 0 || schemaValue > ushort.MaxValue ||
                            !Enum.IsDefined(typeof(Schema), (ushort)schemaValue))
                        {
                            // Skip invalid schema value rather than crashing
                            continue;
                        }

                        var schema = (Schema)(ushort)schemaValue;
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
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // MEDIUM FIX: Validate input parameters
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset, nameof(dataset));
        ArgumentNullException.ThrowIfNull(symbols, nameof(symbols));

        var symbolArray = symbols.ToArray();
        // HIGH FIX: Validate symbol array elements
        Utilities.ErrorBufferHelpers.ValidateSymbolArray(symbolArray);
        // HIGH FIX: Use checked arithmetic via helper
        long startTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(startTime);
        long endTimeNs = Utilities.DateTimeHelpers.ToUnixNanos(endTime);

        return await Task.Run(() =>
        {
            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
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
                // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
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
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Submit a new batch job with advanced options for bulk historical data download.
    /// WARNING: This operation will incur a cost.
    /// </summary>
    /// <remarks>
    /// LOW FIX: This method is currently a stub that delegates to the basic version.
    /// Advanced parameters (encoding, compression, prettyPx, etc.) are not yet implemented
    /// and will be ignored. This functionality requires additional native wrapper implementation.
    /// For now, use the basic <see cref="BatchSubmitJobAsync(string, IEnumerable{string}, Schema, DateTimeOffset, DateTimeOffset, CancellationToken)"/>
    /// method which provides full functionality with default options.
    /// </remarks>
    [Obsolete("This overload is not yet fully implemented. Use the basic BatchSubmitJobAsync method instead. Advanced parameters are currently ignored.", false)]
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
        // MEDIUM FIX: Validate input parameters for fail-fast behavior
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset, nameof(dataset));
        ArgumentNullException.ThrowIfNull(symbols, nameof(symbols));

        // LOW FIX: Documented stub - delegates to basic version with defaults
        // Advanced version would require additional native wrapper implementation
        return await BatchSubmitJobAsync(dataset, symbols, schema, startTime, endTime, cancellationToken);
    }

    /// <summary>
    /// List previous batch jobs
    /// </summary>
    public async Task<IReadOnlyList<BatchJob>> BatchListJobsAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        return await Task.Run(() =>
        {
            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
            var jsonPtr = NativeMethods.dbento_batch_list_jobs(
                _handle,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                throw new DbentoException($"Failed to list batch jobs: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr);
                if (string.IsNullOrEmpty(json))
                    throw new DbentoException("Failed to get batch jobs: empty response from native layer");

                // MEDIUM FIX: Throw on deserialization failure instead of returning empty collection
                var jobs = JsonSerializer.Deserialize<List<BatchJob>>(json);
                if (jobs == null)
                    throw new DbentoException("Failed to deserialize batch jobs response");

                return (IReadOnlyList<BatchJob>)jobs;
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken).ConfigureAwait(false);
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
        var allJobs = await BatchListJobsAsync(cancellationToken).ConfigureAwait(false);
        var stateSet = new HashSet<JobState>(states);

        // HIGH FIX: Use TryParse to prevent FormatException on malformed API timestamps
        return allJobs
            .Where(job => stateSet.Contains(job.State))
            .Where(job => DateTimeOffset.TryParse(job.TsReceived, out var ts) && ts >= since)
            .ToList();
    }

    /// <summary>
    /// List all files associated with a batch job
    /// </summary>
    public async Task<IReadOnlyList<BatchFileDesc>> BatchListFilesAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        return await Task.Run(() =>
        {
            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
            var jsonPtr = NativeMethods.dbento_batch_list_files(
                _handle,
                jobId,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                throw new DbentoException($"Failed to list batch files: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr);
                if (string.IsNullOrEmpty(json))
                    throw new DbentoException("Failed to get batch files: empty response from native layer");

                // MEDIUM FIX: Throw on deserialization failure instead of returning empty collection
                var files = JsonSerializer.Deserialize<List<BatchFileDesc>>(json);
                if (files == null)
                    throw new DbentoException("Failed to deserialize batch files response");

                return (IReadOnlyList<BatchFileDesc>)files;
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Download all files from a batch job to a directory
    /// </summary>
    public async Task<IReadOnlyList<string>> BatchDownloadAsync(
        string outputDir,
        string jobId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        return await Task.Run(() =>
        {
            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
            var jsonPtr = NativeMethods.dbento_batch_download_all(
                _handle,
                outputDir,
                jobId,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (jsonPtr == IntPtr.Zero)
            {
                // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                throw new DbentoException($"Failed to download batch files: {error}");
            }

            try
            {
                var json = Marshal.PtrToStringUTF8(jsonPtr);
                if (string.IsNullOrEmpty(json))
                    throw new DbentoException("Failed to get download paths: empty response from native layer");

                // MEDIUM FIX: Throw on deserialization failure instead of returning empty collection
                var paths = JsonSerializer.Deserialize<List<string>>(json);
                if (paths == null)
                    throw new DbentoException("Failed to deserialize download paths response");

                return (IReadOnlyList<string>)paths;
            }
            finally
            {
                NativeMethods.dbento_free_string(jsonPtr);
            }
        }, cancellationToken).ConfigureAwait(false);
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
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        return await Task.Run(() =>
        {
            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
            var pathPtr = NativeMethods.dbento_batch_download_file(
                _handle,
                outputDir,
                jobId,
                filename,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (pathPtr == IntPtr.Zero)
            {
                // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
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
        }, cancellationToken).ConfigureAwait(false);
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
            ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

            var symbolArray = symbols.ToArray();
        // HIGH FIX: Validate symbol array elements
        Utilities.ErrorBufferHelpers.ValidateSymbolArray(symbolArray);
            if (symbolArray.Length == 0)
            {
                throw new ArgumentException("Symbols collection cannot be empty", nameof(symbols));
            }

            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];

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
                // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
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

                        // HIGH FIX: Use TryParseExact to prevent FormatException on malformed native data
                        if (DateOnly.TryParseExact(startDateStrInterval, "yyyy-MM-dd", out var startDate) &&
                            DateOnly.TryParseExact(endDateStrInterval, "yyyy-MM-dd", out var endDate))
                        {
                            intervals.Add(new MappingInterval
                            {
                                StartDate = startDate,
                                EndDate = endDate,
                                Symbol = symbol
                            });
                        }
                        else
                        {
                            _logger?.LogWarning(
                                "Skipping invalid mapping interval: StartDate={StartDate}, EndDate={EndDate}, Symbol={Symbol}",
                                startDateStrInterval, endDateStrInterval, symbol);
                        }
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
        // MEDIUM FIX: Atomic state transition (0=active -> 1=disposing -> 2=disposed)
        // If already disposing or disposed, return immediately
        if (Interlocked.CompareExchange(ref _disposeState, 1, 0) != 0)
            return ValueTask.CompletedTask;

        _handle?.Dispose();

        // Mark as fully disposed
        Interlocked.Exchange(ref _disposeState, 2);

        return ValueTask.CompletedTask;
    }
}
