namespace Databento.Client.DataSources;

/// <summary>
/// Controls playback speed for backtesting and file replay.
/// </summary>
public readonly struct PlaybackSpeed : IEquatable<PlaybackSpeed>
{
    /// <summary>
    /// The speed multiplier. Values:
    /// - PositiveInfinity: As fast as possible (no delays)
    /// - 1.0: Real-time (1:1 with original timestamps)
    /// - 2.0: Twice as fast
    /// - 0.5: Half speed
    /// </summary>
    public double Multiplier { get; }

    private PlaybackSpeed(double multiplier)
    {
        if (multiplier <= 0 && !double.IsPositiveInfinity(multiplier))
            throw new ArgumentOutOfRangeException(nameof(multiplier), "Multiplier must be positive or infinity");

        Multiplier = multiplier;
    }

    /// <summary>
    /// As fast as possible - no delays between records.
    /// Use this for maximum throughput during backtesting.
    /// </summary>
    public static PlaybackSpeed Maximum => new(double.PositiveInfinity);

    /// <summary>
    /// Real-time playback - matches original timestamps exactly.
    /// Use this to simulate live market conditions.
    /// </summary>
    public static PlaybackSpeed RealTime => new(1.0);

    /// <summary>
    /// Custom speed multiplier.
    /// </summary>
    /// <param name="multiplier">Speed multiplier (2.0 = twice as fast, 0.5 = half speed)</param>
    /// <returns>PlaybackSpeed with the specified multiplier</returns>
    /// <exception cref="ArgumentOutOfRangeException">If multiplier is not positive</exception>
    public static PlaybackSpeed Times(double multiplier) => new(multiplier);

    /// <summary>
    /// Whether this is maximum speed (no delays).
    /// </summary>
    public bool IsMaximum => double.IsPositiveInfinity(Multiplier);

    /// <summary>
    /// Calculate delay between two records based on playback speed.
    /// </summary>
    /// <param name="previousTimestamp">Timestamp of previous record</param>
    /// <param name="currentTimestamp">Timestamp of current record</param>
    /// <returns>Delay to apply, or TimeSpan.Zero if no delay needed</returns>
    public TimeSpan CalculateDelay(DateTimeOffset previousTimestamp, DateTimeOffset currentTimestamp)
    {
        if (IsMaximum)
            return TimeSpan.Zero;

        var elapsed = currentTimestamp - previousTimestamp;
        if (elapsed <= TimeSpan.Zero)
            return TimeSpan.Zero;

        var delay = TimeSpan.FromTicks((long)(elapsed.Ticks / Multiplier));
        return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
    }

    /// <summary>
    /// Calculate delay between two records based on playback speed (nanosecond timestamps).
    /// </summary>
    /// <param name="previousNanos">Previous timestamp in nanoseconds since Unix epoch</param>
    /// <param name="currentNanos">Current timestamp in nanoseconds since Unix epoch</param>
    /// <returns>Delay to apply, or TimeSpan.Zero if no delay needed</returns>
    public TimeSpan CalculateDelay(long previousNanos, long currentNanos)
    {
        if (IsMaximum)
            return TimeSpan.Zero;

        var elapsedNanos = currentNanos - previousNanos;
        if (elapsedNanos <= 0)
            return TimeSpan.Zero;

        // Convert nanoseconds to ticks (1 tick = 100 nanoseconds)
        var elapsedTicks = elapsedNanos / 100;
        var delayTicks = (long)(elapsedTicks / Multiplier);

        return delayTicks > 0 ? TimeSpan.FromTicks(delayTicks) : TimeSpan.Zero;
    }

    public override string ToString() => IsMaximum ? "Maximum" : $"{Multiplier:F1}x";

    public bool Equals(PlaybackSpeed other) => Multiplier.Equals(other.Multiplier);
    public override bool Equals(object? obj) => obj is PlaybackSpeed other && Equals(other);
    public override int GetHashCode() => Multiplier.GetHashCode();

    public static bool operator ==(PlaybackSpeed left, PlaybackSpeed right) => left.Equals(right);
    public static bool operator !=(PlaybackSpeed left, PlaybackSpeed right) => !left.Equals(right);
}
