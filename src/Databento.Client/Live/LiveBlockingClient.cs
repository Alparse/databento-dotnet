using System.Text.Json;
using Databento.Client.Models;
using Encoding = System.Text.Encoding;
using Databento.Client.Models.Dbn;
using Databento.Interop;
using Databento.Interop.Handles;
using Databento.Interop.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Databento.Client.Live;

/// <summary>
/// Pull-based blocking live client implementation matching databento-cpp LiveBlocking API
/// </summary>
public sealed class LiveBlockingClient : ILiveBlockingClient
{
    private readonly ILogger<ILiveBlockingClient> _logger;
    private readonly LiveClientHandle _handle;
    private readonly string? _dataset;
    private readonly bool _sendTsOut;
    private readonly VersionUpgradePolicy _upgradePolicy;
    private readonly TimeSpan _heartbeatInterval;
    private readonly List<(string dataset, Schema schema, string[] symbols, bool withSnapshot, DateTimeOffset? startTime)> _subscriptions = new();
    private bool _isDisposed;
    private bool _isStarted;

    #region Configuration Properties

    /// <summary>
    /// The default dataset for subscriptions, if configured
    /// </summary>
    public string? Dataset => _dataset;

    /// <summary>
    /// Whether ts_out timestamps are sent with records
    /// </summary>
    public bool SendTsOut => _sendTsOut;

    /// <summary>
    /// The DBN version upgrade policy
    /// </summary>
    public VersionUpgradePolicy UpgradePolicy => _upgradePolicy;

    /// <summary>
    /// The heartbeat interval for connection monitoring
    /// </summary>
    public TimeSpan HeartbeatInterval => _heartbeatInterval;

    /// <summary>
    /// The active subscriptions on this client
    /// </summary>
    public IReadOnlyList<LiveSubscription> Subscriptions =>
        _subscriptions.Select(s => new LiveSubscription
        {
            Dataset = s.dataset,
            Schema = s.schema,
            STypeIn = SType.RawSymbol,
            Symbols = s.symbols,
            StartTime = s.startTime,
            WithSnapshot = s.withSnapshot
        }).ToList().AsReadOnly();

    #endregion

    /// <summary>
    /// Create a new LiveBlockingClient (use LiveBlockingClientBuilder instead)
    /// </summary>
    internal LiveBlockingClient(
        string apiKey,
        string? dataset,
        bool sendTsOut,
        VersionUpgradePolicy upgradePolicy,
        TimeSpan heartbeatInterval,
        ILogger<ILiveBlockingClient>? logger)
    {
        _dataset = dataset;
        _sendTsOut = sendTsOut;
        _upgradePolicy = upgradePolicy;
        _heartbeatInterval = heartbeatInterval;
        _logger = logger ?? NullLogger<ILiveBlockingClient>.Instance;

        var errorBuffer = new byte[4096];
        var ptr = NativeMethods.dbento_live_blocking_create_ex(
            apiKey,
            dataset ?? string.Empty,
            sendTsOut ? 1 : 0,
            (int)upgradePolicy,
            (int)heartbeatInterval.TotalSeconds,
            errorBuffer,
            (nuint)errorBuffer.Length);

        if (ptr == IntPtr.Zero)
        {
            var error = Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
            throw new DbentoException($"Failed to create LiveBlocking client: {error}");
        }

        _handle = new LiveClientHandle(ptr);
        _logger.LogInformation("LiveBlockingClient created");
    }

    /// <inheritdoc/>
    public async Task SubscribeAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var symbolArray = symbols.ToArray();
        if (symbolArray.Length == 0)
            throw new ArgumentException("Symbols array cannot be empty", nameof(symbols));

        await Task.Run(() =>
        {
            var errorBuffer = new byte[4096];
            var result = NativeMethods.dbento_live_blocking_subscribe(
                _handle,
                dataset,
                schema.ToSchemaString(),
                symbolArray,
                (nuint)symbolArray.Length,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (result != 0)
            {
                var error = Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Subscribe failed: {error}");
            }

            _logger.LogInformation("Subscribed to {Dataset} {Schema} with {Count} symbols",
                dataset, schema, symbolArray.Length);

            // Track subscription
            _subscriptions.Add((dataset, schema, symbolArray, withSnapshot: false, startTime: null));
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SubscribeWithReplayAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset start,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var symbolArray = symbols.ToArray();
        if (symbolArray.Length == 0)
            throw new ArgumentException("Symbols array cannot be empty", nameof(symbols));

        var startTimeNs = start.ToUnixTimeMilliseconds() * 1_000_000;

        await Task.Run(() =>
        {
            var errorBuffer = new byte[4096];
            var result = NativeMethods.dbento_live_blocking_subscribe_with_replay(
                _handle,
                dataset,
                schema.ToSchemaString(),
                symbolArray,
                (nuint)symbolArray.Length,
                startTimeNs,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (result != 0)
            {
                var error = Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"SubscribeWithReplay failed: {error}");
            }

            _logger.LogInformation("Subscribed with replay from {Start} to {Dataset} {Schema}",
                start, dataset, schema);

            // Track subscription
            _subscriptions.Add((dataset, schema, symbolArray, withSnapshot: false, startTime: start));
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SubscribeWithSnapshotAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var symbolArray = symbols.ToArray();
        if (symbolArray.Length == 0)
            throw new ArgumentException("Symbols array cannot be empty", nameof(symbols));

        await Task.Run(() =>
        {
            var errorBuffer = new byte[4096];
            var result = NativeMethods.dbento_live_blocking_subscribe_with_snapshot(
                _handle,
                dataset,
                schema.ToSchemaString(),
                symbolArray,
                (nuint)symbolArray.Length,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (result != 0)
            {
                var error = Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"SubscribeWithSnapshot failed: {error}");
            }

            _logger.LogInformation("Subscribed with snapshot to {Dataset} {Schema}",
                dataset, schema);

            // Track subscription
            _subscriptions.Add((dataset, schema, symbolArray, withSnapshot: true, startTime: null));
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<DbnMetadata> StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_isStarted)
            throw new InvalidOperationException("Client already started");

        _logger.LogInformation("Starting LiveBlocking client (will block until metadata received)...");

        // This blocks in the native code until metadata is received
        var metadata = await Task.Run(() =>
        {
            var metadataBuffer = new byte[65536]; // 64KB for metadata JSON
            var errorBuffer = new byte[4096];

            var result = NativeMethods.dbento_live_blocking_start(
                _handle,
                metadataBuffer,
                (nuint)metadataBuffer.Length,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (result != 0)
            {
                var error = Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Start failed: {error}");
            }

            // Parse JSON metadata
            var json = Encoding.UTF8.GetString(metadataBuffer).TrimEnd('\0');
            _logger.LogDebug("Received metadata JSON: {Json}", json);

            return ParseMetadata(json);
        }, cancellationToken).ConfigureAwait(false);

        _isStarted = true;
        _logger.LogInformation("LiveBlocking started. Metadata: version={Version}, dataset={Dataset}",
            metadata.Version, metadata.Dataset);

        return metadata;
    }

    /// <inheritdoc/>
    public async Task<Record?> NextRecordAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (!_isStarted)
            throw new InvalidOperationException("Client not started. Call StartAsync() first.");

        var timeoutMs = timeout.HasValue
            ? (int)timeout.Value.TotalMilliseconds
            : -1; // -1 means infinite

        return await Task.Run(() =>
        {
            var recordBuffer = new byte[65536]; // 64KB for record
            var errorBuffer = new byte[4096];

            var result = NativeMethods.dbento_live_blocking_next_record(
                _handle,
                recordBuffer,
                (nuint)recordBuffer.Length,
                out var recordLength,
                out var recordType,
                timeoutMs,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (result == 1)
            {
                // Timeout reached
                return null;
            }

            if (result != 0)
            {
                var error = Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"NextRecord failed: {error}");
            }

            // Parse the record using the same method as LiveClient
            var recordData = new byte[recordLength];
            Array.Copy(recordBuffer, recordData, (int)recordLength);

            return Record.FromBytes(recordData, recordType);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        await Task.Run(() =>
        {
            var errorBuffer = new byte[4096];
            var result = NativeMethods.dbento_live_blocking_reconnect(
                _handle,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (result != 0)
            {
                var error = Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Reconnect failed: {error}");
            }

            _logger.LogInformation("Reconnected");
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ResubscribeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        await Task.Run(() =>
        {
            var errorBuffer = new byte[4096];
            var result = NativeMethods.dbento_live_blocking_resubscribe(
                _handle,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (result != 0)
            {
                var error = Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                throw new DbentoException($"Resubscribe failed: {error}");
            }

            _logger.LogInformation("Resubscribed");
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed) return;

        await Task.Run(() =>
        {
            NativeMethods.dbento_live_blocking_stop(_handle);
            _logger.LogInformation("LiveBlocking stopped");
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;

        try
        {
            await StopAsync().ConfigureAwait(false);
        }
        catch
        {
            // Ignore errors during cleanup
        }

        NativeMethods.dbento_live_blocking_destroy(_handle.DangerousGetHandle());
        _handle.Dispose();

        _isDisposed = true;
        _logger.LogInformation("LiveBlockingClient disposed");
    }

    private static DbnMetadata ParseMetadata(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
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

            return new DbnMetadata
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
        }
        catch (Exception ex)
        {
            throw new DbentoException($"Failed to parse metadata JSON: {ex.Message}", ex);
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
}
