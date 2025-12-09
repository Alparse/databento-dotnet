using Databento.Client.DataSources;
using Databento.Client.DataSources.Caching;
using Databento.Client.Live;
using Microsoft.Extensions.Logging;

namespace Databento.Client.Builders;

/// <summary>
/// Builder for creating backtesting clients that stream historical or file data
/// through the ILiveClient interface.
/// </summary>
/// <remarks>
/// <para>
/// Use this builder to create clients for backtesting that work identically to live clients:
/// </para>
/// <code>
/// // Historical backtesting
/// await using var client = new BacktestingClientBuilder()
///     .WithKeyFromEnv()
///     .WithTimeRange(start, end)
///     .Build();
///
/// // File-based backtesting
/// await using var client = new BacktestingClientBuilder()
///     .WithFileSource("/path/to/data.dbn")
///     .Build();
/// </code>
/// </remarks>
public sealed class BacktestingClientBuilder
{
    private string? _apiKey;
    private string? _dataset;
    private DateTimeOffset? _startTime;
    private DateTimeOffset? _endTime;
    private string? _fileSourcePath;
    private PlaybackSpeed _playbackSpeed = PlaybackSpeed.Maximum;
    private CachePolicy _cachePolicy = CachePolicy.None;
    private string? _cacheDirectory;
    private ILogger? _logger;

    /// <summary>
    /// Set the Databento API key (required for historical backtesting).
    /// </summary>
    public BacktestingClientBuilder WithApiKey(string apiKey)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        return this;
    }

    /// <summary>
    /// Set the API key from the DATABENTO_API_KEY environment variable.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the environment variable is not set</exception>
    public BacktestingClientBuilder WithKeyFromEnv()
    {
        _apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
            ?? throw new InvalidOperationException(
                "DATABENTO_API_KEY environment variable is not set. " +
                "Set the environment variable or use WithApiKey() instead.");
        return this;
    }

    /// <summary>
    /// Set the default dataset for subscriptions.
    /// </summary>
    /// <param name="dataset">Dataset name (e.g., "EQUS.MINI", "XNAS.ITCH")</param>
    public BacktestingClientBuilder WithDataset(string dataset)
    {
        _dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
        return this;
    }

    /// <summary>
    /// Set the time range for historical backtesting.
    /// </summary>
    /// <param name="start">Start time for backtesting</param>
    /// <param name="end">End time for backtesting</param>
    public BacktestingClientBuilder WithTimeRange(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
            throw new ArgumentException("End time must be after start time", nameof(end));

        _startTime = start;
        _endTime = end;
        return this;
    }

    /// <summary>
    /// Stream data from a local DBN file instead of the historical API.
    /// Does not require an API key.
    /// </summary>
    /// <param name="filePath">Path to the DBN file</param>
    public BacktestingClientBuilder WithFileSource(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"DBN file not found: {filePath}", filePath);

        _fileSourcePath = filePath;
        return this;
    }

    /// <summary>
    /// Set the playback speed for backtesting.
    /// </summary>
    /// <param name="speed">Playback speed (default: Maximum)</param>
    public BacktestingClientBuilder WithPlaybackSpeed(PlaybackSpeed speed)
    {
        _playbackSpeed = speed;
        return this;
    }

    /// <summary>
    /// Enable in-memory caching for historical data.
    /// Allows multiple replay runs without re-fetching from API.
    /// </summary>
    public BacktestingClientBuilder WithMemoryCache()
    {
        _cachePolicy = CachePolicy.Memory;
        return this;
    }

    /// <summary>
    /// Enable disk caching for historical data.
    /// Persists data as DBN files for unlimited replay.
    /// </summary>
    /// <param name="directory">Cache directory (optional, defaults to platform-specific location)</param>
    public BacktestingClientBuilder WithDiskCache(string? directory = null)
    {
        _cachePolicy = CachePolicy.Disk;
        _cacheDirectory = directory;
        return this;
    }

    /// <summary>
    /// Set the logger for operational diagnostics.
    /// </summary>
    public BacktestingClientBuilder WithLogger(ILogger logger)
    {
        _logger = logger;
        return this;
    }

    /// <summary>
    /// Build the backtesting client.
    /// </summary>
    /// <returns>An ILiveClient configured for backtesting</returns>
    public ILiveClient Build()
    {
        IDataSource dataSource;

        // File source takes precedence
        if (!string.IsNullOrEmpty(_fileSourcePath))
        {
            dataSource = new FileDataSource(_fileSourcePath, _playbackSpeed, _logger);
        }
        // Historical source
        else if (_startTime.HasValue && _endTime.HasValue)
        {
            if (string.IsNullOrEmpty(_apiKey))
                throw new InvalidOperationException("API key is required for historical backtesting. Call WithApiKey() or WithKeyFromEnv().");

            dataSource = new HistoricalDataSource(
                _apiKey,
                _startTime.Value,
                _endTime.Value,
                _playbackSpeed,
                _logger);
        }
        else
        {
            throw new InvalidOperationException(
                "Either WithTimeRange() or WithFileSource() must be called before Build().");
        }

        return new BacktestingClient(dataSource, _dataset, _logger);
    }
}
