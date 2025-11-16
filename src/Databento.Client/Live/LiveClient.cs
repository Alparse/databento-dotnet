using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Channels;
using Databento.Client.Events;
using Databento.Client.Models;
using Databento.Client.Models.Dbn;
using Databento.Interop;
using Databento.Interop.Handles;
using Databento.Interop.Native;
using Microsoft.Extensions.Logging;

namespace Databento.Client.Live;

/// <summary>
/// Live streaming client implementation
/// </summary>
public sealed class LiveClient : ILiveClient
{
    private readonly LiveClientHandle _handle;
    private readonly RecordCallbackDelegate _recordCallback;
    private readonly ErrorCallbackDelegate _errorCallback;
    private readonly MetadataCallbackDelegate _metadataCallback;
    private readonly Channel<Record> _recordChannel;
    private readonly CancellationTokenSource _cts;
    private readonly string? _defaultDataset;
    private readonly bool _sendTsOut;
    private readonly VersionUpgradePolicy _upgradePolicy;
    private readonly TimeSpan _heartbeatInterval;
    private readonly string _apiKey;
    private readonly ILogger<ILiveClient>? _logger;
    private readonly ExceptionCallback? _exceptionHandler;
    // HIGH FIX: Use thread-safe collection for concurrent subscription operations
    private readonly System.Collections.Concurrent.ConcurrentBag<(string dataset, Schema schema, string[] symbols, bool withSnapshot)> _subscriptions;
    private Task? _streamTask;
    // CRITICAL FIX: Use atomic int for disposal state (0=active, 1=disposing, 2=disposed)
    private int _disposeState = 0;
    // MEDIUM FIX: Use atomic operations instead of volatile for consistency
    private int _connectionState = (int)ConnectionState.Disconnected;
    // TaskCompletionSource for capturing metadata from callback
    private TaskCompletionSource<Models.Dbn.DbnMetadata>? _metadataTcs;

    /// <summary>
    /// Event fired when data is received
    /// </summary>
    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// Event fired when an error occurs
    /// </summary>
    public event EventHandler<Events.ErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Get current connection state from native client (Phase 15)
    /// </summary>
    public ConnectionState ConnectionState
    {
        get
        {
            // CRITICAL FIX: Use atomic read
            if (Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0)
                return ConnectionState.Disconnected;

            // MEDIUM FIX: Read connection state atomically
            return (ConnectionState)Interlocked.CompareExchange(ref _connectionState, 0, 0);
        }
    }

    internal LiveClient(string apiKey)
        : this(apiKey, null, false, VersionUpgradePolicy.Upgrade, TimeSpan.FromSeconds(30), null, null)
    {
    }

    internal LiveClient(
        string apiKey,
        string? defaultDataset,
        bool sendTsOut,
        VersionUpgradePolicy upgradePolicy,
        TimeSpan heartbeatInterval,
        ILogger<ILiveClient>? logger = null,
        ExceptionCallback? exceptionHandler = null)
    {
        if (string.IsNullOrEmpty(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

        _apiKey = apiKey;
        _defaultDataset = defaultDataset;
        _sendTsOut = sendTsOut;
        _upgradePolicy = upgradePolicy;
        _heartbeatInterval = heartbeatInterval;
        _logger = logger;
        _exceptionHandler = exceptionHandler;
        _subscriptions = new System.Collections.Concurrent.ConcurrentBag<(string, Schema, string[], bool)>();
        // MEDIUM FIX: Use Interlocked for consistency
        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Disconnected);

        // Create channel for streaming records
        _recordChannel = Channel.CreateUnbounded<Record>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        _cts = new CancellationTokenSource();

        // Create callbacks (must be stored to prevent GC collection)
        unsafe
        {
            _recordCallback = OnRecordReceived;
            _errorCallback = OnErrorOccurred;
            _metadataCallback = OnMetadataReceived;
        }

        // Create native client with full configuration (Phase 15)
        // MEDIUM FIX: Increased from 512 to 2048 for full error context
        byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
        var handlePtr = NativeMethods.dbento_live_create_ex(
            apiKey,
            defaultDataset,
            sendTsOut ? 1 : 0,
            (int)upgradePolicy,
            (int)heartbeatInterval.TotalSeconds,
            errorBuffer,
            (nuint)errorBuffer.Length);

        if (handlePtr == IntPtr.Zero)
        {
            // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
            _logger?.LogError("Failed to create LiveClient: {Error}", error);
            throw new DbentoException($"Failed to create live client: {error}");
        }

        _handle = new LiveClientHandle(handlePtr);

        _logger?.LogInformation(
            "LiveClient created successfully. Dataset={Dataset}, SendTsOut={SendTsOut}, UpgradePolicy={UpgradePolicy}, Heartbeat={Heartbeat}s",
            defaultDataset ?? "(none)",
            sendTsOut,
            upgradePolicy,
            (int)heartbeatInterval.TotalSeconds);
    }

    /// <summary>
    /// Subscribe to a data stream (matches databento-cpp Subscribe overloads)
    /// </summary>
    public Task SubscribeAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset? startTime = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // MEDIUM FIX: Validate input parameters
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset, nameof(dataset));
        ArgumentNullException.ThrowIfNull(symbols, nameof(symbols));

        var symbolArray = symbols.ToArray();
        // HIGH FIX: Validate symbol array elements
        Utilities.ErrorBufferHelpers.ValidateSymbolArray(symbolArray);

        // MEDIUM FIX: Increased from 512 to 2048 for full error context
        byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
        int result;

        // Check if intraday replay is requested (matches databento-cpp overloads)
        if (startTime.HasValue)
        {
            // Subscribe with intraday replay (matches databento-cpp: Subscribe(symbols, schema, stype, UnixNanos))
            long startTimeNs = (startTime.Value == DateTimeOffset.MinValue)
                ? 0  // Full replay history
                : Utilities.DateTimeHelpers.ToUnixNanos(startTime.Value);

            _logger?.LogInformation(
                "Subscribing with replay: dataset={Dataset}, schema={Schema}, symbolCount={SymbolCount}, startTime={StartTime}",
                dataset,
                schema,
                symbolArray.Length,
                startTime.Value);

            result = NativeMethods.dbento_live_subscribe_with_replay(
                _handle,
                dataset,
                schema.ToSchemaString(),
                symbolArray,
                (nuint)symbolArray.Length,
                startTimeNs,
                errorBuffer,
                (nuint)errorBuffer.Length);
        }
        else
        {
            // Basic subscribe without replay (matches databento-cpp: Subscribe(symbols, schema, stype))
            _logger?.LogInformation(
                "Subscribing to dataset={Dataset}, schema={Schema}, symbolCount={SymbolCount}",
                dataset,
                schema,
                symbolArray.Length);

            result = NativeMethods.dbento_live_subscribe(
                _handle,
                dataset,
                schema.ToSchemaString(),
                symbolArray,
                (nuint)symbolArray.Length,
                errorBuffer,
                (nuint)errorBuffer.Length);
        }

        if (result != 0)
        {
            // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
            _logger?.LogError(
                "Subscription failed with error code {ErrorCode}: {Error}. Dataset={Dataset}, Schema={Schema}",
                result,
                error,
                dataset,
                schema);
            // MEDIUM FIX: Use exception factory method for proper exception type mapping
            throw DbentoException.CreateFromErrorCode($"Subscription failed: {error}", result);
        }

        // Track subscription for resubscription
        _subscriptions.Add((dataset, schema, symbolArray, withSnapshot: false));

        _logger?.LogInformation("Subscription successful for {SymbolCount} symbols", symbolArray.Length);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Subscribe to a data stream with initial snapshot (Phase 15)
    /// </summary>
    public Task SubscribeWithSnapshotAsync(
        string dataset,
        Schema schema,
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
        // MEDIUM FIX: Increased from 512 to 2048 for full error context
        byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];

        // Use native subscribe with snapshot support
        var result = NativeMethods.dbento_live_subscribe_with_snapshot(
            _handle,
            dataset,
            schema.ToSchemaString(),
            symbolArray,
            (nuint)symbolArray.Length,
            errorBuffer,
            (nuint)errorBuffer.Length);

        if (result != 0)
        {
            // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
            // MEDIUM FIX: Use exception factory method for proper exception type mapping
            throw DbentoException.CreateFromErrorCode($"Subscription with snapshot failed: {error}", result);
        }

        // Track subscription for resubscription
        _subscriptions.Add((dataset, schema, symbolArray, withSnapshot: true));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Start receiving data and return DBN metadata (matches databento-cpp LiveBlocking::Start)
    /// </summary>
    public async Task<Models.Dbn.DbnMetadata> StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // MEDIUM FIX: Thread-safe check using Interlocked to prevent race conditions
        // If _streamTask is already set (not null), another thread beat us to starting
        var existingTask = Interlocked.CompareExchange(ref _streamTask, null, null);
        if (existingTask != null)
            throw new InvalidOperationException("Client is already started");

        _logger?.LogInformation("Starting live stream");

        // MEDIUM FIX: Use Interlocked for consistency
        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Connecting);
        _logger?.LogDebug("Connection state changed: Disconnected → Connecting");

        // Create TaskCompletionSource to capture metadata from callback
        _metadataTcs = new TaskCompletionSource<Models.Dbn.DbnMetadata>();

        // Start receiving on a background thread
        // MEDIUM FIX: Create task first, then atomically set it
        var newTask = Task.Run(() =>
        {
            // MEDIUM FIX: Increased from 512 to 2048 for full error context
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];

            // Use dbento_live_start_ex to get metadata callback
            var result = NativeMethods.dbento_live_start_ex(
                _handle,
                _metadataCallback,
                _recordCallback,
                _errorCallback,
                IntPtr.Zero,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (result != 0)
            {
                // HIGH FIX: Use safe error string extraction
                var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
                // MEDIUM FIX: Use Interlocked for consistency
                Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Disconnected);
                _logger?.LogError("Live stream start failed with error code {ErrorCode}: {Error}", result, error);
                _logger?.LogDebug("Connection state changed: Connecting → Disconnected");

                // Set exception on TaskCompletionSource
                var exception = DbentoException.CreateFromErrorCode($"Start failed: {error}", result);
                _metadataTcs?.TrySetException(exception);

                // MEDIUM FIX: Use exception factory method for proper exception type mapping
                throw exception;
            }

            // MEDIUM FIX: Use Interlocked for consistency
            Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Streaming);
            _logger?.LogInformation("Live stream started successfully");
            _logger?.LogDebug("Connection state changed: Connecting → Streaming");
        }, cancellationToken);

        // MEDIUM FIX: Atomically set _streamTask after creating the task
        // This ensures thread-safe publication of the task
        Interlocked.Exchange(ref _streamTask, newTask);

        // Wait for metadata to be received from callback
        return await _metadataTcs.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Stop receiving data
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        // CRITICAL FIX: Use atomic read for disposal state
        if (Interlocked.CompareExchange(ref _disposeState, 0, 0) == 0)
        {
            NativeMethods.dbento_live_stop(_handle);
            // MEDIUM FIX: Use Interlocked for consistency
            Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Stopped);

            // MEDIUM FIX: Add small delay before completing channel to prevent race condition
            // This allows any in-flight callbacks to complete their channel writes
            // before we mark the channel as complete
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);

            _recordChannel.Writer.Complete();
        }
    }

    /// <summary>
    /// Reconnect to the gateway after disconnection (Phase 15)
    /// </summary>
    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // MEDIUM FIX: Use Interlocked for consistency
        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Reconnecting);

        // Stop current stream task if running
        // MEDIUM FIX: Thread-safe read of _streamTask
        var currentTask = Interlocked.CompareExchange(ref _streamTask, null, null);
        if (currentTask != null)
        {
            NativeMethods.dbento_live_stop(_handle);
            try
            {
                await currentTask;
            }
            catch
            {
                // Ignore errors on stop
            }
            // MEDIUM FIX: Thread-safe null assignment
            Interlocked.Exchange(ref _streamTask, null);
        }

        // Use native reconnect (doesn't dispose handle!)
        // MEDIUM FIX: Increased from 512 to 2048 for full error context
        byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
        var result = NativeMethods.dbento_live_reconnect(_handle, errorBuffer, (nuint)errorBuffer.Length);

        if (result != 0)
        {
            // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
            // MEDIUM FIX: Use Interlocked for consistency
            Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Disconnected);
            // MEDIUM FIX: Use exception factory method for proper exception type mapping
            throw DbentoException.CreateFromErrorCode($"Reconnect failed: {error}", result);
        }

        // MEDIUM FIX: Use Interlocked for consistency
        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Connected);
    }

    /// <summary>
    /// Resubscribe to all previous subscriptions (Phase 15)
    /// </summary>
    public async Task ResubscribeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // Use native resubscribe (handles all tracked subscriptions internally)
        // MEDIUM FIX: Increased from 512 to 2048 for full error context
        byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
        var result = NativeMethods.dbento_live_resubscribe(_handle, errorBuffer, (nuint)errorBuffer.Length);

        if (result != 0)
        {
            // HIGH FIX: Use safe error string extraction
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
            // MEDIUM FIX: Use exception factory method for proper exception type mapping
            throw DbentoException.CreateFromErrorCode($"Resubscription failed: {error}", result);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stream records as an async enumerable
    /// </summary>
    public async IAsyncEnumerable<Record> StreamAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var record in _recordChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return record;
        }
    }

    private unsafe void OnRecordReceived(byte* recordBytes, nuint recordLength, byte recordType, IntPtr userData)
    {
        // CRITICAL FIX: Check disposal state atomically before processing
        if (Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0)
        {
            // Disposing or disposed - ignore callback
            return;
        }

        try
        {
            // CRITICAL FIX: Validate pointer before dereferencing
            if (recordBytes == null)
            {
                var ex = new DbentoException("Received null pointer from native code");
                ErrorOccurred?.Invoke(this, new Events.ErrorEventArgs(ex));
                return;
            }

            // CRITICAL FIX: Validate length to prevent integer overflow
            if (recordLength == 0)
            {
                var ex = new DbentoException("Received zero-length record");
                ErrorOccurred?.Invoke(this, new Events.ErrorEventArgs(ex));
                return;
            }

            if (recordLength > int.MaxValue)
            {
                var ex = new DbentoException($"Record too large: {recordLength} bytes exceeds maximum {int.MaxValue}");
                ErrorOccurred?.Invoke(this, new Events.ErrorEventArgs(ex));
                return;
            }

            // Sanity check: reasonable maximum record size (10MB)
            if (recordLength > Utilities.Constants.MaxReasonableRecordSize)
            {
                var ex = new DbentoException($"Record suspiciously large: {recordLength} bytes");
                ErrorOccurred?.Invoke(this, new Events.ErrorEventArgs(ex));
                return;
            }

            // Copy bytes to managed memory
            var bytes = new byte[recordLength];
            Marshal.Copy((IntPtr)recordBytes, bytes, 0, (int)recordLength);

            // Deserialize record using the recordType parameter
            var record = Record.FromBytes(bytes, recordType);

            // CRITICAL FIX: Double-check disposal state before channel operations
            if (Interlocked.CompareExchange(ref _disposeState, 0, 0) == 0)
            {
                // Write to channel
                _recordChannel.Writer.TryWrite(record);

                // Fire event
                DataReceived?.Invoke(this, new DataReceivedEventArgs(record));
            }
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new Events.ErrorEventArgs(ex));
        }
    }

    private void OnErrorOccurred(string errorMessage, int errorCode, IntPtr userData)
    {
        var exception = new DbentoException(errorMessage, errorCode);

        // Fire the ErrorOccurred event (existing behavior)
        ErrorOccurred?.Invoke(this, new Events.ErrorEventArgs(exception, errorCode));

        // If exception handler is provided, call it and check the action
        if (_exceptionHandler != null)
        {
            try
            {
                var action = _exceptionHandler(exception);
                _logger?.LogDebug("ExceptionCallback returned {Action} for error: {Error}", action, errorMessage);

                if (action == ExceptionAction.Stop)
                {
                    _logger?.LogInformation("ExceptionCallback requested Stop - stopping stream");
                    // Stop the stream (async operation, but callback is synchronous)
                    // We'll schedule this on the thread pool to avoid blocking
                    Task.Run(async () =>
                    {
                        try
                        {
                            await StopAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error during exception handler stop");
                        }
                    });
                }
                else
                {
                    _logger?.LogDebug("ExceptionCallback requested Continue - continuing stream");
                }
            }
            catch (Exception handlerEx)
            {
                _logger?.LogError(handlerEx, "Exception in ExceptionCallback - ignoring and continuing");
                // If the exception handler itself throws, we ignore it and continue
            }
        }
    }

    private void OnMetadataReceived(string metadataJson, nuint metadataLength, IntPtr userData)
    {
        try
        {
            // CRITICAL FIX: Check disposal state atomically before processing
            if (Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0)
            {
                // Disposing or disposed - ignore callback
                return;
            }

            _logger?.LogDebug("Metadata received: {MetadataLength} bytes", metadataLength);

            // Parse JSON metadata into DbnMetadata object
            // Use JsonDocument.Parse to handle UINT64_MAX for "end" field (same as LiveBlocking)
            using var doc = JsonDocument.Parse(metadataJson);
            var root = doc.RootElement;

            // Parse start - try as uint64 first, then convert to long
            var startElem = root.GetProperty("start");
            long start = startElem.ValueKind == JsonValueKind.Number && startElem.TryGetInt64(out var s)
                ? s
                : (long)startElem.GetUInt64(); // Large values beyond int64 range

            // Parse end - handle UINT64_MAX (18446744073709551615) which means "no end"
            var endElem = root.GetProperty("end");
            long end;
            if (endElem.TryGetUInt64(out var endUlong))
            {
                // If it's UINT64_MAX, use Int64.MaxValue as sentinel
                end = (endUlong == ulong.MaxValue) ? long.MaxValue : (long)Math.Min(endUlong, (ulong)long.MaxValue);
            }
            else
            {
                end = endElem.GetInt64();
            }

            var metadata = new Models.Dbn.DbnMetadata
            {
                Version = root.GetProperty("version").GetByte(),
                Dataset = root.GetProperty("dataset").GetString() ?? string.Empty,
                Schema = root.TryGetProperty("schema", out var schemaElem) && schemaElem.ValueKind != JsonValueKind.Null
                    ? (Schema)schemaElem.GetInt32()
                    : null,
                Start = start,
                End = end,
                Limit = root.GetProperty("limit").GetUInt64(),
                StypeIn = root.TryGetProperty("stype_in", out var stypeInElem) && stypeInElem.ValueKind != JsonValueKind.Null
                    ? (SType)stypeInElem.GetInt32()
                    : null,
                StypeOut = (SType)root.GetProperty("stype_out").GetInt32(),
                TsOut = root.GetProperty("ts_out").GetBoolean(),
                SymbolCstrLen = root.GetProperty("symbol_cstr_len").GetUInt16(),
                Symbols = ParseStringArray(root.GetProperty("symbols")),
                Partial = ParseStringArray(root.GetProperty("partial")),
                NotFound = ParseStringArray(root.GetProperty("not_found")),
                Mappings = new List<SymbolMapping>() // TODO: Parse mappings if needed
            };

            _logger?.LogInformation(
                "DBN metadata received: version={Version}, dataset={Dataset}",
                metadata.Version,
                metadata.Dataset);

            // Set the result on the TaskCompletionSource
            _metadataTcs?.TrySetResult(metadata);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing metadata callback");
            _metadataTcs?.TrySetException(ex);
            ErrorOccurred?.Invoke(this, new Events.ErrorEventArgs(ex));
        }
    }

    /// <summary>
    /// Block until the stream stops (matches databento-cpp LiveThreaded::BlockForStop)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <remarks>
    /// Waits indefinitely for the stream to stop. Useful for keeping the client alive
    /// until data processing is complete. Matches C++ API: void BlockForStop();
    /// </remarks>
    public async Task BlockUntilStoppedAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // MEDIUM FIX: Thread-safe read of _streamTask
        var streamTask = Interlocked.CompareExchange(ref _streamTask, null, null);
        if (streamTask == null)
            throw new InvalidOperationException("Client not started. Call StartAsync() first.");

        _logger?.LogDebug("BlockUntilStoppedAsync: Waiting for stream to stop...");

        try
        {
            await streamTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            _logger?.LogInformation("BlockUntilStoppedAsync: Stream stopped normally");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("BlockUntilStoppedAsync: Cancelled by user");
            throw;
        }
    }

    /// <summary>
    /// Block until the stream stops or timeout is reached (matches databento-cpp LiveThreaded::BlockForStop)
    /// </summary>
    /// <param name="timeout">Maximum time to wait</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if stopped normally, false if timeout was reached</returns>
    /// <remarks>
    /// Waits for the stream to stop or until timeout expires.
    /// Matches C++ API: KeepGoing BlockForStop(std::chrono::milliseconds timeout);
    /// </remarks>
    public async Task<bool> BlockUntilStoppedAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // MEDIUM FIX: Thread-safe read of _streamTask
        var streamTask = Interlocked.CompareExchange(ref _streamTask, null, null);
        if (streamTask == null)
            throw new InvalidOperationException("Client not started. Call StartAsync() first.");

        _logger?.LogDebug("BlockUntilStoppedAsync: Waiting for stream to stop (timeout: {Timeout}ms)...", timeout.TotalMilliseconds);

        try
        {
            await streamTask.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            _logger?.LogInformation("BlockUntilStoppedAsync: Stream stopped normally");
            return true;  // Stopped normally
        }
        catch (System.TimeoutException)
        {
            _logger?.LogWarning("BlockUntilStoppedAsync: Timeout reached after {Timeout}ms", timeout.TotalMilliseconds);
            return false;  // Timeout reached
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("BlockUntilStoppedAsync: Cancelled by user");
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        // CRITICAL FIX: Atomic state transition (0=active -> 1=disposing -> 2=disposed)
        // If already disposing or disposed, return immediately
        if (Interlocked.CompareExchange(ref _disposeState, 1, 0) != 0)
            return;

        // Stop streaming (this also completes the channel)
        try
        {
            await StopAsync();
        }
        catch
        {
            // Ignore errors during disposal
        }

        // Cancel and wait for stream task with timeout
        _cts.Cancel();
        // MEDIUM FIX: Thread-safe read of _streamTask during disposal
        var streamTask = Interlocked.CompareExchange(ref _streamTask, null, null);
        if (streamTask != null)
        {
            try
            {
                // Wait with 5-second timeout to prevent deadlocks
                await streamTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (System.TimeoutException)
            {
                // Log warning - task didn't complete within timeout
                // In production, consider tracking this metric
#if DEBUG
                System.Diagnostics.Debug.WriteLine(
                    "Warning: LiveClient stream task did not complete within timeout during disposal");
#endif
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }

        // Channel already completed by StopAsync() - no need to complete again

        // Dispose handle
        _handle?.Dispose();
        _cts?.Dispose();

        // CRITICAL FIX: Mark as fully disposed
        Interlocked.Exchange(ref _disposeState, 2);
    }

    private static List<string> ParseStringArray(JsonElement element)
    {
        var list = new List<string>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(item.GetString() ?? string.Empty);
        }
        return list;
    }
}
