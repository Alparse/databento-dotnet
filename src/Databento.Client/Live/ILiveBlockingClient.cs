using Databento.Client.Models;
using Databento.Client.Models.Dbn;

namespace Databento.Client.Live;

/// <summary>
/// Pull-based blocking live client matching databento-cpp LiveBlocking API.
/// Provides synchronous control over record retrieval via StartAsync/NextRecordAsync.
/// </summary>
/// <remarks>
/// This is the pull-based API counterpart to LiveClient (LiveThreaded).
/// Use this when you want explicit control over when records are retrieved,
/// as opposed to push-based event/callback delivery.
/// </remarks>
public interface ILiveBlockingClient : IAsyncDisposable
{
    /// <summary>
    /// Subscribe to a dataset and schema
    /// </summary>
    /// <param name="dataset">Dataset to subscribe to (e.g., "EQUS.MINI")</param>
    /// <param name="schema">Schema type to receive</param>
    /// <param name="symbols">List of symbols to subscribe to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SubscribeAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe with historical replay from a specific start time
    /// </summary>
    /// <param name="dataset">Dataset to subscribe to</param>
    /// <param name="schema">Schema type to receive</param>
    /// <param name="symbols">List of symbols to subscribe to</param>
    /// <param name="start">Start time for historical replay</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SubscribeWithReplayAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset start,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe with market-by-order (MBO) snapshot at subscription time
    /// </summary>
    /// <param name="dataset">Dataset to subscribe to</param>
    /// <param name="schema">Schema type to receive</param>
    /// <param name="symbols">List of symbols to subscribe to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SubscribeWithSnapshotAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Start the live stream and return DBN metadata.
    /// Blocks until metadata is received from the gateway.
    /// Matches C++ LiveBlocking::Start() which returns Metadata.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>DBN metadata about the subscription</returns>
    Task<DbnMetadata> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pull the next record from the stream (blocking call).
    /// Returns null if timeout is reached without receiving a record.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for a record. Use Timeout.InfiniteTimeSpan to wait indefinitely.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Next record, or null if timeout reached</returns>
    Task<Record?> NextRecordAsync(
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconnect to the live gateway
    /// </summary>
    Task ReconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resubscribe to all previous subscriptions
    /// </summary>
    Task ResubscribeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop the live stream
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
