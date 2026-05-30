using Databento.Client.Events;
using Databento.Client.Models;

namespace Databento.Client.Live;

/// <summary>
/// Live streaming client for real-time market data
/// </summary>
/// <remarks>
/// <b>Thread safety:</b> All Subscribe calls must complete before calling <see cref="StartAsync"/>.
/// Subscribe methods are not thread-safe — do not call them concurrently or after the stream has started.
/// Using <c>await</c> on each call naturally serializes them and mitigates this risk.
/// This mirrors the databento-cpp calling convention where thread safety is by design (single-threaded setup),
/// not by enforcement (no internal locks).
/// </remarks>
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
    /// <remarks>
    /// Not thread-safe. Await each call and complete all subscriptions before calling <see cref="StartAsync"/>.
    /// </remarks>
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
    /// Subscribe to a data stream with a specified symbol type
    /// </summary>
    /// <param name="dataset">Dataset name (e.g., "GLBX.MDP3")</param>
    /// <param name="schema">Schema type</param>
    /// <param name="symbols">List of symbols to subscribe to</param>
    /// <param name="stypeIn">Input symbol type (e.g., SType.Continuous for "MNQ.v.0")</param>
    /// <param name="startTime">Optional start time for intraday replay</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SubscribeAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        SType stypeIn,
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
    /// Subscribe to a data stream with initial snapshot and a specified symbol type
    /// </summary>
    /// <param name="dataset">Dataset name (e.g., "GLBX.MDP3")</param>
    /// <param name="schema">Schema type</param>
    /// <param name="symbols">List of symbols to subscribe to</param>
    /// <param name="stypeIn">Input symbol type (e.g., SType.Continuous for "MNQ.v.0")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SubscribeWithSnapshotAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        SType stypeIn,
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
    /// Stream records as an async enumerable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Live mode:</b> The stream runs indefinitely until <see cref="StopAsync"/> is called
    /// or the <paramref name="cancellationToken"/> is cancelled. You must explicitly stop
    /// the client to exit the <c>await foreach</c> loop.
    /// </para>
    /// <para>
    /// <b>Backtest mode:</b> The stream ends automatically when all historical data or
    /// file records have been consumed.
    /// </para>
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token that stops the stream when cancelled</param>
    /// <returns>An async enumerable of records that yields until the client is stopped or data is exhausted</returns>
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
