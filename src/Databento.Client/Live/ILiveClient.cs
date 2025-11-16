using Databento.Client.Events;
using Databento.Client.Models;

namespace Databento.Client.Live;

/// <summary>
/// Live streaming client for real-time market data
/// </summary>
public interface ILiveClient : IAsyncDisposable
{
    /// <summary>
    /// Event fired when data is received
    /// </summary>
    event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>
    /// Event fired when an error occurs
    /// </summary>
    event EventHandler<Events.ErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Subscribe to a data stream (matches databento-cpp Subscribe overloads)
    /// </summary>
    /// <param name="dataset">Dataset name (e.g., "GLBX.MDP3")</param>
    /// <param name="schema">Schema type</param>
    /// <param name="symbols">List of symbols to subscribe to</param>
    /// <param name="startTime">Optional start time for intraday replay. Use DateTimeOffset.MinValue or null for no replay. Corresponds to databento-cpp's UnixNanos parameter.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SubscribeAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset? startTime = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribe to a data stream with initial snapshot
    /// </summary>
    /// <param name="dataset">Dataset name (e.g., "GLBX.MDP3")</param>
    /// <param name="schema">Schema type</param>
    /// <param name="symbols">List of symbols to subscribe to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SubscribeWithSnapshotAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Start receiving data and return DBN metadata (matches databento-cpp LiveBlocking::Start)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>DBN metadata containing version, dataset, and other session information</returns>
    Task<Models.Dbn.DbnMetadata> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop receiving data
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconnect to the gateway after disconnection
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ReconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resubscribe to all previous subscriptions
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ResubscribeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream records as an async enumerable
    /// </summary>
    IAsyncEnumerable<Record> StreamAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Block until the stream stops (matches databento-cpp LiveThreaded::BlockForStop)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <remarks>
    /// Waits indefinitely for the stream to stop. Useful for keeping the client alive
    /// until data processing is complete. Matches C++ API: void BlockForStop();
    /// </remarks>
    Task BlockUntilStoppedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Block until the stream stops or timeout is reached (matches databento-cpp LiveThreaded::BlockForStop)
    /// </summary>
    /// <param name="timeout">Maximum time to wait</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if stopped normally, false if timeout was reached</returns>
    /// <remarks>
    /// Waits for the stream to stop or until timeout expires.
    /// Matches C++ API: KeepGoing BlockForStop(std::chrono::milliseconds timeout);
    /// </remarks>
    Task<bool> BlockUntilStoppedAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}
