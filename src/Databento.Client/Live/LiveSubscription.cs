using Databento.Client.Models;

namespace Databento.Client.Live;

/// <summary>
/// Represents an active subscription on a live client
/// </summary>
public sealed class LiveSubscription
{
    /// <summary>
    /// The dataset for this subscription
    /// </summary>
    public required string Dataset { get; init; }

    /// <summary>
    /// The schema being streamed
    /// </summary>
    public required Schema Schema { get; init; }

    /// <summary>
    /// The input symbol type used for the subscription
    /// </summary>
    public required SType STypeIn { get; init; }

    /// <summary>
    /// The symbols included in this subscription
    /// </summary>
    public required IReadOnlyList<string> Symbols { get; init; }

    /// <summary>
    /// The start time for intraday replay, or null for real-time streaming
    /// </summary>
    public DateTimeOffset? StartTime { get; init; }

    /// <summary>
    /// Whether a snapshot was requested at subscription start
    /// </summary>
    public bool WithSnapshot { get; init; }

    /// <summary>
    /// Returns a string representation of this subscription
    /// </summary>
    public override string ToString()
    {
        var symbols = Symbols.Count <= 3
            ? string.Join(",", Symbols)
            : $"{Symbols[0]},{Symbols[1]},...({Symbols.Count} total)";

        var mode = StartTime.HasValue ? $"replay from {StartTime:HH:mm}" : "live";
        var snapshot = WithSnapshot ? " +snapshot" : "";

        return $"{Dataset}/{Schema} [{symbols}] ({mode}{snapshot})";
    }
}
