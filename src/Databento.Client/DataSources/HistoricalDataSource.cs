using System.Runtime.CompilerServices;
using Databento.Client.Builders;
using Databento.Client.Historical;
using Databento.Client.Live;
using Databento.Client.Models;
using Databento.Client.Models.Dbn;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Databento.Client.DataSources;

/// <summary>
/// Data source that replays historical data for backtesting.
/// Fetches data from the Historical API and streams it with optional playback speed control.
/// </summary>
public sealed class HistoricalDataSource : IDataSource
{
    private readonly string _apiKey;
    private readonly DateTimeOffset _startTime;
    private readonly DateTimeOffset _endTime;
    private readonly PlaybackSpeed _playbackSpeed;
    private readonly ILogger _logger;
    private readonly List<LiveSubscription> _subscriptions = new();

    private IHistoricalClient? _historicalClient;
    private Dictionary<uint, string>? _symbolMap;
    private int _connectionState = (int)ConnectionState.Disconnected;
    private int _disposeState = 0;
    private CancellationTokenSource? _streamCts;

    /// <summary>
    /// Playback controller for pause/resume/seek operations.
    /// </summary>
    public PlaybackController Playback { get; } = new();

    /// <inheritdoc/>
    public DataSourceCapabilities Capabilities => DataSourceCapabilities.Historical;

    /// <inheritdoc/>
    public ConnectionState State => (ConnectionState)Interlocked.CompareExchange(ref _connectionState, 0, 0);

    /// <inheritdoc/>
    public event EventHandler<DataSourceErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// The configured start time for backtesting.
    /// </summary>
    public DateTimeOffset StartTime => _startTime;

    /// <summary>
    /// The configured end time for backtesting.
    /// </summary>
    public DateTimeOffset EndTime => _endTime;

    /// <summary>
    /// Creates a new historical data source for backtesting.
    /// </summary>
    /// <param name="apiKey">Databento API key</param>
    /// <param name="startTime">Start time for historical data</param>
    /// <param name="endTime">End time for historical data</param>
    /// <param name="playbackSpeed">Playback speed (default: Maximum)</param>
    /// <param name="logger">Logger instance (optional)</param>
    public HistoricalDataSource(
        string apiKey,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        PlaybackSpeed? playbackSpeed = null,
        ILogger? logger = null)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));

        if (endTime <= startTime)
            throw new ArgumentException("End time must be after start time", nameof(endTime));

        _startTime = startTime;
        _endTime = endTime;
        _playbackSpeed = playbackSpeed ?? PlaybackSpeed.Maximum;
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
        _logger.LogInformation("HistoricalDataSource: Connecting (backtest {Start} to {End})...", _startTime, _endTime);

        // Create historical client
        _historicalClient = new HistoricalClientBuilder()
            .WithApiKey(_apiKey)
            .Build();

        // Resolve symbols for all subscriptions
        _symbolMap = await ResolveSymbolsAsync(cancellationToken).ConfigureAwait(false);

        _streamCts = new CancellationTokenSource();
        Playback.Reset();

        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Connected);
        _logger.LogInformation("HistoricalDataSource: Connected. Resolved {SymbolCount} symbol mappings.", _symbolMap.Count);

        // Build metadata
        var firstSub = _subscriptions[0];
        return new DbnMetadata
        {
            Version = 2, // DBN version
            Dataset = firstSub.Dataset,
            Schema = firstSub.Schema,
            Start = _startTime.ToUnixTimeNanoseconds(),
            End = _endTime.ToUnixTimeNanoseconds(),
            Limit = 0,
            StypeIn = SType.RawSymbol,
            StypeOut = SType.InstrumentId,
            TsOut = false,
            SymbolCstrLen = 22,
            Symbols = _subscriptions.SelectMany(s => s.Symbols).Distinct().ToList(),
            Partial = new List<string>(),
            NotFound = new List<string>(),
            Mappings = new List<SymbolMapping>()
        };
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<Record> StreamAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_historicalClient == null || _symbolMap == null)
            throw new InvalidOperationException("Not connected. Call ConnectAsync() first.");

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _streamCts?.Token ?? CancellationToken.None);

        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Streaming);

        // CRITICAL: Emit SymbolMappingMessage records first (for parity with live)
        foreach (var (instrumentId, symbol) in _symbolMap)
        {
            if (!await Playback.WaitIfPausedAsync(linkedCts.Token).ConfigureAwait(false))
                yield break;

            yield return CreateSymbolMappingMessage(instrumentId, symbol);
        }

        // Stream historical records
        long index = 0;
        long? previousNanos = null;

        foreach (var subscription in _subscriptions)
        {
            await foreach (var record in _historicalClient.GetRangeAsync(
                subscription.Dataset,
                subscription.Schema,
                subscription.Symbols,
                _startTime,
                _endTime,
                linkedCts.Token))
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
                var timestamp = record.Timestamp;
                Playback.UpdatePosition(index, timestamp);
                index++;

                yield return record;
            }
        }

        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Disconnected);
        _logger.LogInformation("HistoricalDataSource: Finished streaming {RecordCount} records", index);
    }

    /// <inheritdoc/>
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _streamCts?.Cancel();
        Playback.Stop();
        Interlocked.Exchange(ref _connectionState, (int)ConnectionState.Disconnected);
        _logger.LogInformation("HistoricalDataSource: Disconnected");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        // Historical source doesn't support true reconnect - throw
        // Users should create a new data source to replay
        throw new NotSupportedException(
            "HistoricalDataSource does not support reconnection. " +
            "Create a new HistoricalDataSource instance to replay from the beginning.");
    }

    private async Task<Dictionary<uint, string>> ResolveSymbolsAsync(CancellationToken cancellationToken)
    {
        var result = new Dictionary<uint, string>();

        // Convert to DateOnly for symbology API
        var startDate = DateOnly.FromDateTime(_startTime.UtcDateTime);
        var endDate = DateOnly.FromDateTime(_endTime.UtcDateTime);

        // Symbology API requires end_date > start_date
        // For intraday backtests (same day), add one day to end_date
        if (endDate <= startDate)
        {
            endDate = startDate.AddDays(1);
        }

        foreach (var sub in _subscriptions)
        {
            try
            {
                var resolution = await _historicalClient!.SymbologyResolveAsync(
                    sub.Dataset,
                    sub.Symbols,
                    SType.RawSymbol,
                    SType.InstrumentId,
                    startDate,
                    endDate,
                    cancellationToken).ConfigureAwait(false);

                foreach (var (inputSymbol, intervals) in resolution.Mappings)
                {
                    foreach (var interval in intervals)
                    {
                        if (uint.TryParse(interval.Symbol, out var instrumentId))
                        {
                            result[instrumentId] = inputSymbol;
                        }
                    }
                }

                _logger.LogDebug("HistoricalDataSource: Resolved {Count} mappings for {Dataset}",
                    resolution.Mappings.Count, sub.Dataset);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HistoricalDataSource: Failed to resolve symbols for {Dataset}", sub.Dataset);
                // Continue with other subscriptions - partial resolution is better than none
            }
        }

        return result;
    }

    private SymbolMappingMessage CreateSymbolMappingMessage(uint instrumentId, string symbol)
    {
        return new SymbolMappingMessage
        {
            InstrumentId = instrumentId,
            TimestampNs = _startTime.ToUnixTimeNanoseconds(),
            STypeIn = SType.RawSymbol,
            STypeInSymbol = symbol,
            STypeOut = SType.InstrumentId,
            STypeOutSymbol = symbol,
            StartTs = _startTime.ToUnixTimeNanoseconds(),
            EndTs = _endTime.ToUnixTimeNanoseconds()
        };
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposeState, 1, 0) != 0)
            return;

        _streamCts?.Cancel();
        _streamCts?.Dispose();

        if (_historicalClient != null)
        {
            await _historicalClient.DisposeAsync().ConfigureAwait(false);
        }

        Interlocked.Exchange(ref _disposeState, 2);
    }
}

/// <summary>
/// Extension methods for DateTimeOffset.
/// </summary>
internal static class DateTimeOffsetExtensions
{
    /// <summary>
    /// Converts DateTimeOffset to nanoseconds since Unix epoch.
    /// </summary>
    public static long ToUnixTimeNanoseconds(this DateTimeOffset dto)
    {
        return dto.ToUnixTimeMilliseconds() * 1_000_000;
    }
}
