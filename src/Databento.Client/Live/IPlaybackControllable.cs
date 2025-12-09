using Databento.Client.DataSources;

namespace Databento.Client.Live;

/// <summary>
/// Interface for clients that support playback control (pause, resume, seek).
/// Implemented by LiveClient when using a data source that supports playback.
/// </summary>
/// <remarks>
/// <para>
/// Use pattern matching to check if playback control is available:
/// </para>
/// <code>
/// if (client is IPlaybackControllable controllable)
/// {
///     controllable.Playback.Pause();
///     Console.WriteLine($"Paused at index {controllable.Playback.CurrentIndex}");
///     controllable.Playback.Resume();
/// }
/// </code>
/// <para>
/// Playback control is only available when using backtesting data sources
/// (HistoricalDataSource or FileDataSource). Live data sources do not support playback control.
/// </para>
/// </remarks>
public interface IPlaybackControllable
{
    /// <summary>
    /// The playback controller for pause/resume/seek operations.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// Thrown when the underlying data source does not support playback control.
    /// </exception>
    PlaybackController Playback { get; }
}
