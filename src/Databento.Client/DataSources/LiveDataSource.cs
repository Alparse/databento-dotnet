using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Channels;
using Databento.Client.Live;
using Databento.Client.Models;
using Databento.Client.Models.Dbn;
using Databento.Interop;
using Databento.Interop.Handles;
using Databento.Interop.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Databento.Client.DataSources;

/// <summary>
/// Data source that connects to Databento's live gateway.
/// This is the production data source for real-time market data.
/// </summary>
public sealed class LiveDataSource : IDataSource
{
    private readonly string _apiKey;
    private readonly string? _defaultDataset;
    private readonly bool _sendTsOut;
    private readonly VersionUpgradePolicy _upgradePolicy;
    private readonly TimeSpan _heartbeatInterval;
    private readonly ILogger _logger;
    private readonly List<LiveSubscription> _subscriptions = new();

    private LiveClientHandle? _handle;
    private Channel<Record>? _recordChannel;
    private TaskCompletionSource<DbnMetadata>? _metadataTcs;
    private int _connectionState = (int)ConnectionState.Disconnected;
    private int _disposeState = 0;

    // Native callbacks (must be stored to prevent GC collection)
    private RecordCallbackDelegate? _recordCallback;
    private ErrorCallbackDelegate? _errorCallback;
    private MetadataCallbackDelegate? _metadataCallback;

    /// <inheritdoc/>
    public DataSourceCapabilities Capabilities => DataSourceCapabilities.Live;

    /// <inheritdoc/>
    public ConnectionState State => (ConnectionState)Interlocked.CompareExchange(ref _connectionState, 0, 0);

    /// <inheritdoc/>
    public event EventHandler<DataSourceErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Creates a new live data source.
    /// </summary>
    /// <param name="apiKey">Databento API key</param>
    /// <param name="defaultDataset">Default dataset for subscriptions (optional)</param>
    /// <param name="sendTsOut">Whether to include ts_out timestamps</param>
    /// <param name="upgradePolicy">DBN version upgrade policy</param>
    /// <param name="heartbeatInterval">Heartbeat interval for connection monitoring</param>
    /// <param name="logger">Logger instance (optional)</param>
    public LiveDataSource(
        string apiKey,
        string? defaultDataset = null,
        bool sendTsOut = false,
        VersionUpgradePolicy upgradePolicy = VersionUpgradePolicy.Upgrade,
        TimeSpan? heartbeatInterval = null,
        ILogger? logger = null)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _defaultDataset = defaultDataset;
        _sendTsOut = sendTsOut;
        _upgradePolicy = upgradePolicy;
        _heartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(30);
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc/>
    public void AddSubscription(LiveSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        _subscriptions.Add(subscription);
    }

    /// <inheritdoc/>
    public IReadOnlyList<LiveSubscription> GetSubscriptions() => _subscriptions.AsReadOnly();

    /// <inheritdoc/>
    public async Task<DbnMetadata> ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        if (_subscriptions.Count == 0)
            throw new InvalidOperationException("No subscriptions configured. Call AddSubscription() before ConnectAsync().");

        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Connecting);
        _logger.LogInformation("LiveDataSource: Connecting to gateway...");

        // Create native handle
        byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
        var handlePtr = NativeMethods.dbento_live_create_ex(
            _apiKey,
            _defaultDataset,
            _sendTsOut ? 1 : 0,
            (int)_upgradePolicy,
            (int)_heartbeatInterval.TotalSeconds,
            errorBuffer,
            (nuint)errorBuffer.Length);

        if (handlePtr == IntPtr.Zero)
        {
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
            Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Disconnected);
            throw new DbentoException($"Failed to create live client: {error}");
        }

        _handle = new LiveClientHandle(handlePtr);

        // Create channel for streaming
        _recordChannel = Channel.CreateUnbounded<Record>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        // Create callbacks
        unsafe
        {
            _recordCallback = OnRecordReceived;
            _errorCallback = OnErrorOccurred;
            _metadataCallback = OnMetadataReceived;
        }

        // Apply subscriptions
        foreach (var sub in _subscriptions)
        {
            await ApplySubscriptionAsync(sub, cancellationToken).ConfigureAwait(false);
        }

        // Create TaskCompletionSource for metadata
        _metadataTcs = new TaskCompletionSource<DbnMetadata>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Start native streaming
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
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
            Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Disconnected);
            _metadataTcs.TrySetException(new DbentoException($"Start failed: {error}"));
            throw DbentoException.CreateFromErrorCode($"Start failed: {error}", result);
        }

        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Streaming);
        _logger.LogInformation("LiveDataSource: Connected and streaming");

        // Wait for metadata
        return await _metadataTcs.Task.ConfigureAwait(false);
    }

    private Task ApplySubscriptionAsync(LiveSubscription sub, CancellationToken cancellationToken)
    {
        if (_handle == null)
            throw new InvalidOperationException("Handle not initialized");

        var symbolArray = sub.Symbols.ToArray();
        byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
        int result;

        if (sub.WithSnapshot)
        {
            result = NativeMethods.dbento_live_subscribe_with_snapshot(
                _handle,
                sub.Dataset,
                sub.Schema.ToSchemaString(),
                symbolArray,
                (nuint)symbolArray.Length,
                errorBuffer,
                (nuint)errorBuffer.Length);
        }
        else if (sub.StartTime.HasValue)
        {
            long startTimeNs = sub.StartTime.Value == DateTimeOffset.MinValue
                ? 0
                : Utilities.DateTimeHelpers.ToUnixNanos(sub.StartTime.Value);

            result = NativeMethods.dbento_live_subscribe_with_replay(
                _handle,
                sub.Dataset,
                sub.Schema.ToSchemaString(),
                symbolArray,
                (nuint)symbolArray.Length,
                startTimeNs,
                errorBuffer,
                (nuint)errorBuffer.Length);
        }
        else
        {
            result = NativeMethods.dbento_live_subscribe(
                _handle,
                sub.Dataset,
                sub.Schema.ToSchemaString(),
                symbolArray,
                (nuint)symbolArray.Length,
                errorBuffer,
                (nuint)errorBuffer.Length);
        }

        if (result != 0)
        {
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
            throw DbentoException.CreateFromErrorCode($"Subscription failed: {error}", result);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Record> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_recordChannel == null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync() first.");

        await foreach (var record in _recordChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return record;
        }
    }

    /// <inheritdoc/>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0)
            return Task.CompletedTask;

        if (_handle != null)
        {
            byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
            NativeMethods.dbento_live_stop_and_wait(
                _handle,
                10000,
                errorBuffer,
                (nuint)errorBuffer.Length);
        }

        _recordChannel?.Writer.TryComplete();
        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Disconnected);
        _logger.LogInformation("LiveDataSource: Disconnected");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        if (_handle == null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync() first.");

        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Reconnecting);

        byte[] errorBuffer = new byte[Utilities.Constants.ErrorBufferSize];
        var result = NativeMethods.dbento_live_reconnect(_handle, errorBuffer, (nuint)errorBuffer.Length);

        if (result != 0)
        {
            var error = Utilities.ErrorBufferHelpers.SafeGetString(errorBuffer);
            Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Disconnected);
            throw DbentoException.CreateFromErrorCode($"Reconnect failed: {error}", result);
        }

        // Recreate channel
        _recordChannel = Channel.CreateUnbounded<Record>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Connected);
        _logger.LogInformation("LiveDataSource: Reconnected");
        return Task.CompletedTask;
    }

    private unsafe void OnRecordReceived(byte* recordBytes, nuint recordLength, byte recordType, IntPtr userData)
    {
        if (Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0)
            return;

        try
        {
            if (recordBytes == null || recordLength == 0 || recordLength > int.MaxValue)
                return;

            var bytes = new byte[recordLength];
            Marshal.Copy((IntPtr)recordBytes, bytes, 0, (int)recordLength);

            var record = Record.FromBytes(bytes, recordType);
            _recordChannel?.Writer.TryWrite(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LiveDataSource: Error processing record callback");
            ErrorOccurred?.Invoke(this, new DataSourceErrorEventArgs(ex));
        }
    }

    private void OnErrorOccurred(string errorMessage, int errorCode, IntPtr userData)
    {
        var exception = new DbentoException(errorMessage, errorCode);
        _logger.LogError(exception, "LiveDataSource: Error from gateway: {ErrorCode}", errorCode);
        ErrorOccurred?.Invoke(this, new DataSourceErrorEventArgs(exception, isRecoverable: true, errorCode));
    }

    private void OnMetadataReceived(string metadataJson, nuint metadataLength, IntPtr userData)
    {
        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            var root = doc.RootElement;

            var startElem = root.GetProperty("start");
            long start = startElem.ValueKind == JsonValueKind.Number && startElem.TryGetInt64(out var s)
                ? s
                : (long)startElem.GetUInt64();

            var endElem = root.GetProperty("end");
            long end;
            if (endElem.TryGetUInt64(out var endUlong))
            {
                end = (endUlong == ulong.MaxValue) ? long.MaxValue : (long)Math.Min(endUlong, (ulong)long.MaxValue);
            }
            else
            {
                end = endElem.GetInt64();
            }

            var metadata = new DbnMetadata
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
                Mappings = new List<SymbolMapping>()
            };

            _metadataTcs?.TrySetResult(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LiveDataSource: Error processing metadata callback");
            _metadataTcs?.TrySetException(ex);
        }
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

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposeState, 1, 0) != 0)
            return;

        await DisconnectAsync().ConfigureAwait(false);
        _handle?.Dispose();
        Interlocked.Exchange(ref _disposeState, 2);
    }
}
