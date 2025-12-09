using System.Runtime.CompilerServices;
using Databento.Client.DataSources;
using Databento.Client.Events;
using Databento.Client.Models;
using Databento.Client.Models.Dbn;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Databento.Client.Live;

/// <summary>
/// A client that implements ILiveClient but streams from a data source (historical or file).
/// Enables backtesting with identical user code to live trading.
/// </summary>
public sealed class BacktestingClient : ILiveClient, IPlaybackControllable
{
    private readonly IDataSource _dataSource;
    private readonly ILogger _logger;
    private readonly List<LiveSubscription> _subscriptions = new();

    private DbnMetadata? _metadata;
    private int _connectionState = (int)ConnectionState.Disconnected;
    private int _disposeState = 0;

    /// <inheritdoc/>
    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <inheritdoc/>
    public event EventHandler<Events.ErrorEventArgs>? ErrorOccurred;

    /// <inheritdoc/>
    public ConnectionState ConnectionState => (ConnectionState)Interlocked.CompareExchange(ref _connectionState, 0, 0);

    /// <inheritdoc/>
    public string? Dataset { get; }

    /// <inheritdoc/>
    public bool SendTsOut => false;

    /// <inheritdoc/>
    public VersionUpgradePolicy UpgradePolicy => VersionUpgradePolicy.Upgrade;

    /// <inheritdoc/>
    public TimeSpan HeartbeatInterval => TimeSpan.Zero;

    /// <inheritdoc/>
    public IReadOnlyList<LiveSubscription> Subscriptions => _subscriptions.AsReadOnly();

    /// <summary>
    /// Playback controller for pause/resume/seek operations.
    /// </summary>
    public PlaybackController Playback
    {
        get
        {
            return _dataSource switch
            {
                HistoricalDataSource hds => hds.Playback,
                FileDataSource fds => fds.Playback,
                _ => throw new NotSupportedException("Playback control is not supported by this data source")
            };
        }
    }

    /// <summary>
    /// The underlying data source.
    /// </summary>
    public IDataSource DataSource => _dataSource;

    /// <summary>
    /// Creates a new backtesting client with the specified data source.
    /// </summary>
    /// <param name="dataSource">The data source to stream from</param>
    /// <param name="dataset">Default dataset name (optional)</param>
    /// <param name="logger">Logger instance (optional)</param>
    public BacktestingClient(IDataSource dataSource, string? dataset = null, ILogger? logger = null)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        Dataset = dataset;
        _logger = logger ?? NullLogger.Instance;

        // Forward errors from data source
        _dataSource.ErrorOccurred += (s, e) =>
            ErrorOccurred?.Invoke(this, new Events.ErrorEventArgs(e.Exception, e.ErrorCode));
    }

    /// <inheritdoc/>
    public Task SubscribeAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset? startTime = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        var subscription = new LiveSubscription
        {
            Dataset = dataset,
            Schema = schema,
            STypeIn = SType.RawSymbol,
            Symbols = symbols.ToList(),
            StartTime = startTime
        };

        _subscriptions.Add(subscription);
        _dataSource.AddSubscription(subscription);

        _logger.LogDebug("BacktestingClient: Added subscription {Dataset}/{Schema}", dataset, schema);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SubscribeWithSnapshotAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        // Snapshots not supported in backtesting
        _logger.LogWarning("BacktestingClient: SubscribeWithSnapshotAsync called but snapshots not supported in backtesting");
        return SubscribeAsync(dataset, schema, symbols, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<DbnMetadata> StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        if (_subscriptions.Count == 0)
            throw new InvalidOperationException("No subscriptions configured. Call SubscribeAsync() before StartAsync().");

        _logger.LogInformation("BacktestingClient: Starting...");
        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Connecting);

        _metadata = await _dataSource.ConnectAsync(cancellationToken).ConfigureAwait(false);

        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Streaming);
        _logger.LogInformation("BacktestingClient: Started. Dataset: {Dataset}", _metadata.Dataset);

        return _metadata;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0)
            return;

        _logger.LogInformation("BacktestingClient: Stopping...");
        await _dataSource.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Stopped);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Record> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_metadata == null)
            throw new InvalidOperationException("Client not started. Call StartAsync() first.");

        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Streaming);

        await foreach (var record in _dataSource.StreamAsync(cancellationToken))
        {
            // Fire event
            DataReceived?.Invoke(this, new DataReceivedEventArgs(record));
            yield return record;
        }

        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Disconnected);
    }

    /// <inheritdoc/>
    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        if (!_dataSource.Capabilities.SupportsReconnect)
            throw new NotSupportedException("This data source does not support reconnection.");

        _logger.LogInformation("BacktestingClient: Reconnecting...");
        await _dataSource.ReconnectAsync(cancellationToken).ConfigureAwait(false);
        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Connected);
    }

    /// <inheritdoc/>
    public async Task ResubscribeAsync(CancellationToken cancellationToken = default)
    {
        // In backtesting, resubscribe means reconnect
        await ReconnectAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task BlockUntilStoppedAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        // Wait until disconnected
        while (ConnectionState == ConnectionState.Streaming)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> BlockUntilStoppedAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            await BlockUntilStoppedAsync(cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false; // Timeout
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposeState, 1, 0) != 0)
            return;

        try
        {
            await _dataSource.DisconnectAsync().ConfigureAwait(false);
        }
        catch
        {
            // Ignore errors during disposal
        }

        await _dataSource.DisposeAsync().ConfigureAwait(false);
        Interlocked.Exchange(ref _disposeState, 2);
    }
}
