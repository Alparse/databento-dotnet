using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Databento.Client.Events;
using Databento.Client.Models;
using Databento.Interop;
using Databento.Interop.Handles;
using Databento.Interop.Native;

namespace Databento.Client.Live;

/// <summary>
/// Live streaming client implementation
/// </summary>
public sealed class LiveClient : ILiveClient
{
    private readonly LiveClientHandle _handle;
    private readonly RecordCallbackDelegate _recordCallback;
    private readonly ErrorCallbackDelegate _errorCallback;
    private readonly Channel<Record> _recordChannel;
    private readonly CancellationTokenSource _cts;
    private readonly string? _defaultDataset;
    private readonly bool _sendTsOut;
    private readonly VersionUpgradePolicy _upgradePolicy;
    private readonly TimeSpan _heartbeatInterval;
    private readonly string _apiKey;
    private readonly List<(string dataset, Schema schema, string[] symbols, bool withSnapshot)> _subscriptions;
    private Task? _streamTask;
    private bool _disposed;
    private volatile ConnectionState _connectionState;

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
            if (_disposed)
                return ConnectionState.Disconnected;

            var state = NativeMethods.dbento_live_get_connection_state(_handle);
            return state switch
            {
                0 => ConnectionState.Disconnected,
                1 => ConnectionState.Connecting,
                2 => ConnectionState.Connected,
                3 => ConnectionState.Streaming,
                _ => ConnectionState.Disconnected
            };
        }
    }

    internal LiveClient(string apiKey)
        : this(apiKey, null, false, VersionUpgradePolicy.Upgrade, TimeSpan.FromSeconds(30))
    {
    }

    internal LiveClient(
        string apiKey,
        string? defaultDataset,
        bool sendTsOut,
        VersionUpgradePolicy upgradePolicy,
        TimeSpan heartbeatInterval)
    {
        if (string.IsNullOrEmpty(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

        _apiKey = apiKey;
        _defaultDataset = defaultDataset;
        _sendTsOut = sendTsOut;
        _upgradePolicy = upgradePolicy;
        _heartbeatInterval = heartbeatInterval;
        _subscriptions = new List<(string, Schema, string[], bool)>();
        _connectionState = ConnectionState.Disconnected;

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
        }

        // Create native client with full configuration (Phase 15)
        byte[] errorBuffer = new byte[512];
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
            var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
            throw new DbentoException($"Failed to create live client: {error}");
        }

        _handle = new LiveClientHandle(handlePtr);
    }

    /// <summary>
    /// Subscribe to a data stream
    /// </summary>
    public Task SubscribeAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var symbolArray = symbols.ToArray();
        byte[] errorBuffer = new byte[512];

        var result = NativeMethods.dbento_live_subscribe(
            _handle,
            dataset,
            schema.ToSchemaString(),
            symbolArray,
            (nuint)symbolArray.Length,
            errorBuffer,
            (nuint)errorBuffer.Length);

        if (result != 0)
        {
            var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
            throw new DbentoException($"Subscription failed: {error}", result);
        }

        // Track subscription for resubscription
        _subscriptions.Add((dataset, schema, symbolArray, withSnapshot: false));

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
        ObjectDisposedException.ThrowIf(_disposed, this);

        var symbolArray = symbols.ToArray();
        byte[] errorBuffer = new byte[512];

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
            var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
            throw new DbentoException($"Subscription with snapshot failed: {error}", result);
        }

        // Track subscription for resubscription
        _subscriptions.Add((dataset, schema, symbolArray, withSnapshot: true));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Start receiving data
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_streamTask != null)
            throw new InvalidOperationException("Client is already started");

        _connectionState = ConnectionState.Connecting;

        // Start receiving on a background thread
        _streamTask = Task.Run(() =>
        {
            byte[] errorBuffer = new byte[512];

            var result = NativeMethods.dbento_live_start(
                _handle,
                _recordCallback,
                _errorCallback,
                IntPtr.Zero,
                errorBuffer,
                (nuint)errorBuffer.Length);

            if (result != 0)
            {
                var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                _connectionState = ConnectionState.Disconnected;
                throw new DbentoException($"Start failed: {error}", result);
            }

            _connectionState = ConnectionState.Streaming;
        }, cancellationToken);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop receiving data
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_disposed)
        {
            NativeMethods.dbento_live_stop(_handle);
            _connectionState = ConnectionState.Stopped;
            _recordChannel.Writer.Complete();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reconnect to the gateway after disconnection (Phase 15)
    /// </summary>
    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _connectionState = ConnectionState.Reconnecting;

        // Stop current stream task if running
        if (_streamTask != null)
        {
            NativeMethods.dbento_live_stop(_handle);
            try
            {
                await _streamTask;
            }
            catch
            {
                // Ignore errors on stop
            }
            _streamTask = null;
        }

        // Use native reconnect (doesn't dispose handle!)
        byte[] errorBuffer = new byte[512];
        var result = NativeMethods.dbento_live_reconnect(_handle, errorBuffer, (nuint)errorBuffer.Length);

        if (result != 0)
        {
            var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
            _connectionState = ConnectionState.Disconnected;
            throw new DbentoException($"Reconnect failed: {error}", result);
        }

        _connectionState = ConnectionState.Connected;
    }

    /// <summary>
    /// Resubscribe to all previous subscriptions (Phase 15)
    /// </summary>
    public async Task ResubscribeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Use native resubscribe (handles all tracked subscriptions internally)
        byte[] errorBuffer = new byte[512];
        var result = NativeMethods.dbento_live_resubscribe(_handle, errorBuffer, (nuint)errorBuffer.Length);

        if (result != 0)
        {
            var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
            throw new DbentoException($"Resubscription failed: {error}", result);
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
        try
        {
            // Copy bytes to managed memory
            var bytes = new byte[recordLength];
            Marshal.Copy((IntPtr)recordBytes, bytes, 0, (int)recordLength);

            // Deserialize record using the recordType parameter
            var record = Record.FromBytes(bytes, recordType);

            // Write to channel
            _recordChannel.Writer.TryWrite(record);

            // Fire event
            DataReceived?.Invoke(this, new DataReceivedEventArgs(record));
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new Events.ErrorEventArgs(ex));
        }
    }

    private void OnErrorOccurred(string errorMessage, int errorCode, IntPtr userData)
    {
        var exception = new DbentoException(errorMessage, errorCode);
        ErrorOccurred?.Invoke(this, new Events.ErrorEventArgs(exception, errorCode));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;

        // Stop streaming
        await StopAsync();

        // Cancel and wait for stream task with timeout
        _cts.Cancel();
        if (_streamTask != null)
        {
            try
            {
                // Wait with 5-second timeout to prevent deadlocks
                await _streamTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
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

        // Complete channel
        _recordChannel.Writer.Complete();

        // Dispose handle
        _handle?.Dispose();
        _cts?.Dispose();
    }
}
