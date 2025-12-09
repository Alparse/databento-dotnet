using Databento.Client.Live;
using Databento.Client.Models;
using Databento.Client.Models.Dbn;

namespace Databento.Client.DataSources;

/// <summary>
/// Abstraction for market data sources (live gateway, historical API, files).
/// Enables backtesting by allowing LiveClient to receive records from different sources.
/// </summary>
public interface IDataSource : IAsyncDisposable
{
    /// <summary>
    /// Capabilities of this data source.
    /// </summary>
    DataSourceCapabilities Capabilities { get; }

    /// <summary>
    /// Current connection state.
    /// </summary>
    ConnectionState State { get; }

    /// <summary>
    /// Event fired when an error occurs in the data source.
    /// </summary>
    event EventHandler<DataSourceErrorEventArgs>? ErrorOccurred;

    /// <summary>
    /// Configure a subscription before connecting.
    /// Called by LiveClient.SubscribeAsync().
    /// </summary>
    /// <param name="subscription">The subscription to add</param>
    void AddSubscription(LiveSubscription subscription);

    /// <summary>
    /// Get all configured subscriptions.
    /// </summary>
    IReadOnlyList<LiveSubscription> GetSubscriptions();

    /// <summary>
    /// Connect to the data source and begin receiving data.
    /// Returns metadata about the session.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>DBN metadata about the connection</returns>
    Task<DbnMetadata> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream records from the source.
    /// Must include SymbolMappingMessage records at the start for symbol resolution parity with live.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of records</returns>
    IAsyncEnumerable<Record> StreamAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect from the data source.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconnect after disconnection (if supported).
    /// Check <see cref="DataSourceCapabilities.SupportsReconnect"/> before calling.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="NotSupportedException">If reconnection is not supported</exception>
    Task ReconnectAsync(CancellationToken cancellationToken = default);
}
