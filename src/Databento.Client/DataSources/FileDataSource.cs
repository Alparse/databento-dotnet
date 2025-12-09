using System.Runtime.CompilerServices;
using Databento.Client.Dbn;
using Databento.Client.Live;
using Databento.Client.Models;
using Databento.Client.Models.Dbn;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Databento.Client.DataSources;

/// <summary>
/// Data source that reads from local DBN files.
/// Enables offline backtesting and replay of pre-downloaded data.
/// </summary>
public sealed class FileDataSource : IDataSource
{
    private readonly string _filePath;
    private readonly PlaybackSpeed _playbackSpeed;
    private readonly ILogger _logger;
    private readonly List<LiveSubscription> _subscriptions = new();

    private DbnFileReader? _reader;
    private DbnMetadata? _metadata;
    private int _connectionState = (int)ConnectionState.Disconnected;
    private int _disposeState = 0;
    private CancellationTokenSource? _streamCts;

    /// <summary>
    /// Playback controller for pause/resume/seek operations.
    /// </summary>
    public PlaybackController Playback { get; } = new();

    /// <inheritdoc/>
    public DataSourceCapabilities Capabilities => DataSourceCapabilities.File;

    /// <inheritdoc/>
    public ConnectionState State => (ConnectionState)Interlocked.CompareExchange(ref _connectionState, 0, 0);

    /// <inheritdoc/>
    public event EventHandler<DataSourceErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// The path to the DBN file.
    /// </summary>
    public string FilePath => _filePath;

    /// <summary>
    /// Creates a new file data source.
    /// </summary>
    /// <param name="filePath">Path to the DBN file</param>
    /// <param name="playbackSpeed">Playback speed (default: Maximum)</param>
    /// <param name="logger">Logger instance (optional)</param>
    /// <exception cref="FileNotFoundException">If the file does not exist</exception>
    public FileDataSource(
        string filePath,
        PlaybackSpeed? playbackSpeed = null,
        ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"DBN file not found: {filePath}", filePath);

        _filePath = filePath;
        _playbackSpeed = playbackSpeed ?? PlaybackSpeed.Maximum;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc/>
    public void AddSubscription(LiveSubscription subscription)
    {
        // File source ignores subscriptions - the file defines the data
        _logger.LogDebug("FileDataSource: AddSubscription ignored - file defines data");
        _subscriptions.Add(subscription);
    }

    /// <inheritdoc/>
    public IReadOnlyList<LiveSubscription> GetSubscriptions() => _subscriptions.AsReadOnly();

    /// <inheritdoc/>
    public Task<DbnMetadata> ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Connecting);
        _logger.LogInformation("FileDataSource: Opening file {FilePath}", _filePath);

        try
        {
            _reader = new DbnFileReader(_filePath);
            _metadata = _reader.GetMetadata();
            _streamCts = new CancellationTokenSource();
            Playback.Reset();

            Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Connected);
            _logger.LogInformation("FileDataSource: Connected. Dataset: {Dataset}, Schema: {Schema}",
                _metadata.Dataset, _metadata.Schema);

            return Task.FromResult(_metadata);
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Disconnected);
            _logger.LogError(ex, "FileDataSource: Failed to open file");
            ErrorOccurred?.Invoke(this, new DataSourceErrorEventArgs(ex));
            throw;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Record> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_reader == null || _metadata == null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync() first.");

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _streamCts?.Token ?? CancellationToken.None);

        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Streaming);

        // Emit symbol mappings from metadata if available
        foreach (var mapping in _metadata.Mappings)
        {
            if (!await Playback.WaitIfPausedAsync(linkedCts.Token).ConfigureAwait(false))
                yield break;

            yield return CreateSymbolMappingMessage(mapping);
        }

        // Stream records from file
        long index = 0;
        long? previousNanos = null;

        await foreach (var record in _reader.ReadRecordsAsync(linkedCts.Token))
        {
            // Check for pause/stop
            if (!await Playback.WaitIfPausedAsync(linkedCts.Token).ConfigureAwait(false))
                yield break;

            // Apply playback speed
            if (!_playbackSpeed.IsMaximum && previousNanos.HasValue)
            {
                var delay = _playbackSpeed.CalculateDelay(previousNanos.Value, record.TimestampNs);
                if (delay > TimeSpan.FromMilliseconds(1))
                {
                    await Task.Delay(delay, linkedCts.Token).ConfigureAwait(false);
                }
            }

            previousNanos = record.TimestampNs;

            // Update playback position
            Playback.UpdatePosition(index, record.Timestamp);
            index++;

            yield return record;
        }

        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Disconnected);
        _logger.LogInformation("FileDataSource: Finished streaming {RecordCount} records", index);
    }

    /// <inheritdoc/>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _streamCts?.Cancel();
        Playback.Stop();

        _reader?.Dispose();
        _reader = null;

        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Disconnected);
        _logger.LogInformation("FileDataSource: Disconnected");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Interlocked.CompareExchange(ref _disposeState, 0, 0) != 0, this);

        _logger.LogInformation("FileDataSource: Reconnecting (replay from start)");

        // Close current reader
        _reader?.Dispose();
        _reader = null;

        // Reset playback
        Playback.Reset();

        // Re-open file
        _reader = new DbnFileReader(_filePath);
        _metadata = _reader.GetMetadata();

        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Connected);
        return Task.CompletedTask;
    }

    private SymbolMappingMessage CreateSymbolMappingMessage(SymbolMapping mapping)
    {
        // Get first interval for instrument ID
        var interval = mapping.Intervals.FirstOrDefault();

        // When StypeOut is InstrumentId, interval.Symbol contains the numeric ID
        uint instrumentId = 0;
        if (interval != null && _metadata?.StypeOut == SType.InstrumentId)
        {
            uint.TryParse(interval.Symbol, out instrumentId);
        }

        return new SymbolMappingMessage
        {
            InstrumentId = instrumentId,
            TimestampNs = _metadata?.Start ?? 0,
            STypeIn = _metadata?.StypeIn ?? SType.RawSymbol,
            STypeInSymbol = mapping.RawSymbol,
            STypeOut = _metadata?.StypeOut ?? SType.InstrumentId,
            STypeOutSymbol = mapping.RawSymbol, // Human-readable ticker symbol
            StartTs = _metadata?.Start ?? 0,
            EndTs = _metadata?.End ?? 0
        };
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposeState, 1, 0) != 0)
            return;

        _streamCts?.Cancel();
        _streamCts?.Dispose();

        _reader?.Dispose();

        Interlocked.Exchange(ref _disposeState, 2);
        await Task.CompletedTask;
    }
}
