using System;

namespace Databento.Client.Resilience;

/// <summary>
/// Configuration for retry behavior on transient failures.
/// Uses exponential backoff with optional jitter.
/// </summary>
public sealed class RetryPolicy
{
    /// <summary>
    /// Maximum number of retry attempts. Default is 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay before first retry. Default is 1 second.
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between retries. Default is 30 seconds.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Multiplier for exponential backoff. Default is 2.0.
    /// Each retry waits: InitialDelay * (BackoffMultiplier ^ attemptNumber)
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Whether to add random jitter to prevent thundering herd.
    /// Adds ±25% randomness to each delay. Default is true.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Default retry policy with sensible defaults.
    /// </summary>
    public static RetryPolicy Default => new();

    /// <summary>
    /// No retry - fail immediately on first error.
    /// </summary>
    public static RetryPolicy None => new() { MaxRetries = 0 };

    /// <summary>
    /// Aggressive retry policy for critical connections.
    /// 5 retries, up to 60 second max delay.
    /// </summary>
    public static RetryPolicy Aggressive => new()
    {
        MaxRetries = 5,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(60),
        BackoffMultiplier = 2.0,
        UseJitter = true
    };

    /// <summary>
    /// Calculate the delay for a given retry attempt (0-based).
    /// </summary>
    /// <param name="attempt">The retry attempt number (0 for first retry)</param>
    /// <returns>The delay to wait before this retry attempt</returns>
    public TimeSpan GetDelay(int attempt)
    {
        if (attempt < 0)
            throw new ArgumentOutOfRangeException(nameof(attempt), "Attempt must be non-negative");

        // Calculate exponential delay
        var exponentialMs = InitialDelay.TotalMilliseconds * Math.Pow(BackoffMultiplier, attempt);

        // Cap at max delay
        var delayMs = Math.Min(exponentialMs, MaxDelay.TotalMilliseconds);

        // Add jitter if enabled (±25%)
        if (UseJitter)
        {
            var jitterFactor = 0.75 + (Random.Shared.NextDouble() * 0.5); // 0.75 to 1.25
            delayMs *= jitterFactor;
        }

        return TimeSpan.FromMilliseconds(delayMs);
    }

    /// <summary>
    /// Check if another retry should be attempted.
    /// </summary>
    /// <param name="attempt">Current attempt number (0-based)</param>
    /// <returns>True if another retry is allowed</returns>
    public bool ShouldRetry(int attempt) => attempt < MaxRetries;
}
