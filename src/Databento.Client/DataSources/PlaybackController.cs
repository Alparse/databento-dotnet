namespace Databento.Client.DataSources;

/// <summary>
/// Controls playback state for backtesting data sources.
/// Supports pause, resume, seek, and position tracking.
/// </summary>
public sealed class PlaybackController
{
    private readonly SemaphoreSlim _pauseSemaphore = new(1, 1);
    private readonly object _lock = new();

    private volatile bool _isPaused;
    private volatile bool _isStopped;
    private long _currentIndex;
    private DateTimeOffset? _currentTimestamp;

    /// <summary>
    /// Current record index (0-based).
    /// </summary>
    public long CurrentIndex => Interlocked.Read(ref _currentIndex);

    /// <summary>
    /// Timestamp of the last yielded record.
    /// </summary>
    public DateTimeOffset? CurrentTimestamp
    {
        get
        {
            lock (_lock)
            {
                return _currentTimestamp;
            }
        }
    }

    /// <summary>
    /// Whether playback is currently paused.
    /// </summary>
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Whether playback has been stopped.
    /// </summary>
    public bool IsStopped => _isStopped;

    /// <summary>
    /// Event fired when playback is paused.
    /// </summary>
    public event EventHandler? Paused;

    /// <summary>
    /// Event fired when playback is resumed.
    /// </summary>
    public event EventHandler? Resumed;

    /// <summary>
    /// Event fired when position changes.
    /// </summary>
    public event EventHandler<PlaybackPositionEventArgs>? PositionChanged;

    /// <summary>
    /// Pause playback. StreamAsync will block until Resume() is called.
    /// </summary>
    public void Pause()
    {
        if (_isPaused || _isStopped)
            return;

        _isPaused = true;
        _pauseSemaphore.Wait(); // Acquire to block streaming
        Paused?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Resume playback after pause.
    /// </summary>
    public void Resume()
    {
        if (!_isPaused)
            return;

        _isPaused = false;
        _pauseSemaphore.Release(); // Release to unblock streaming
        Resumed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Stop playback completely.
    /// </summary>
    public void Stop()
    {
        _isStopped = true;

        // Unblock if paused so stream can exit
        if (_isPaused)
        {
            _isPaused = false;
            try
            {
                _pauseSemaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                // Already released, ignore
            }
        }
    }

    /// <summary>
    /// Get the index to resume from (for seek operations).
    /// </summary>
    public long GetResumeIndex() => CurrentIndex;

    /// <summary>
    /// Set position for next playback (seek).
    /// Note: Seek only affects the position tracking. The data source
    /// must support starting from an arbitrary position for this to be effective.
    /// </summary>
    /// <param name="index">The index to seek to</param>
    public void SeekToIndex(long index)
    {
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be non-negative");

        Interlocked.Exchange(ref _currentIndex, index);
    }

    /// <summary>
    /// Reset to beginning for replay.
    /// </summary>
    public void Reset()
    {
        _isStopped = false;
        _isPaused = false;
        Interlocked.Exchange(ref _currentIndex, 0);

        lock (_lock)
        {
            _currentTimestamp = null;
        }

        // Ensure semaphore is in released state
        if (_pauseSemaphore.CurrentCount == 0)
        {
            try
            {
                _pauseSemaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                // Already released, ignore
            }
        }
    }

    /// <summary>
    /// Called by data source to check if should continue and wait if paused.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if should continue, false if stopped</returns>
    internal async Task<bool> WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        if (_isStopped)
            return false;

        if (_isPaused)
        {
            // Wait for resume
            await _pauseSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            _pauseSemaphore.Release();
        }

        return !_isStopped && !cancellationToken.IsCancellationRequested;
    }

    /// <summary>
    /// Called by data source to update position.
    /// </summary>
    /// <param name="index">Current record index</param>
    /// <param name="timestamp">Current record timestamp</param>
    internal void UpdatePosition(long index, DateTimeOffset timestamp)
    {
        Interlocked.Exchange(ref _currentIndex, index);

        lock (_lock)
        {
            _currentTimestamp = timestamp;
        }

        PositionChanged?.Invoke(this, new PlaybackPositionEventArgs(index, timestamp));
    }
}

/// <summary>
/// Event arguments for playback position changes.
/// </summary>
public sealed class PlaybackPositionEventArgs : EventArgs
{
    /// <summary>
    /// The current record index.
    /// </summary>
    public long Index { get; }

    /// <summary>
    /// The current record timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Creates a new instance of PlaybackPositionEventArgs.
    /// </summary>
    public PlaybackPositionEventArgs(long index, DateTimeOffset timestamp)
    {
        Index = index;
        Timestamp = timestamp;
    }
}
