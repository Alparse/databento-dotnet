using Databento.Client.Models;

namespace Databento.Client.Live;

/// <summary>
/// Represents an active subscription on a live client
/// </summary>
public sealed class LiveSubscription : IEquatable<LiveSubscription?>
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


    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as LiveSubscription);
    /// <inheritdoc/>
    public bool Equals(LiveSubscription? other)
    {
        if (other is null || Schema != other.Schema || STypeIn != other.STypeIn || StartTime != other.StartTime || WithSnapshot != other.WithSnapshot || Dataset != other.Dataset)
            return false;

        return CompareSymbols(Symbols, other.Symbols);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Dataset);
        hash.Add(Schema);
        hash.Add(STypeIn);
        hash.Add(WithSnapshot);
        if (StartTime is not null)
            hash.Add(StartTime);

        if (Symbols is not null)
        {
            // incorporate each symbol into the hash so lists with same elements produce same hash
            foreach (var s in Symbols)
                hash.Add(s);
        }

        return hash.ToHashCode();
    }

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

    /// <summary>
    /// Implements the operator ==.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator ==(LiveSubscription? left, LiveSubscription? right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left is null || right is null)
            return false;

        return left.Equals(right);
    }

    /// <summary>
    /// Implements the operator !=.
    /// </summary>
    /// <param name="left">The left.</param>
    /// <param name="right">The right.</param>
    /// <returns>
    /// The result of the operator.
    /// </returns>
    public static bool operator !=(LiveSubscription? left, LiveSubscription? right) => !(left == right);

    /// <summary>
    /// Compares to lists.
    /// </summary>
    /// <returns></returns>
    private static bool CompareSymbols(IReadOnlyList<string> list1, IReadOnlyList<string> list2)
    {
        if (ReferenceEquals(list1, list2))
            return true;

        if (list1 is null || list2 is null)
            return false;

        if (list1.Count != list2.Count)
            return false;

        // According to the subscription logic, we need to compare ignoring the order and duplicates, but for speed and based on the usage logic - we compare completely.
        for (int i = 0; i < list1.Count; i++)
        {
            if (list1[i] != list2[i])
                return false;
        }

        return true;
    }
}
