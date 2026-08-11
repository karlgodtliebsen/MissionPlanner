namespace MissionPlanner.Core.Replay;

/// <summary>Coordinates one read-only telemetry-log playback session.</summary>
public interface IReplaySessionManager : IReplayClock, IAsyncDisposable
{
    /// <summary>Gets the current immutable replay snapshot.</summary>
    ReplaySessionSnapshot Snapshot { get; }

    /// <summary>Occurs after replay state, clock, or projected vehicles change.</summary>
    event EventHandler<ReplaySessionChangedEventArgs>? Changed;

    /// <summary>Loads and takes ownership of a seekable telemetry-log stream.</summary>
    /// <param name="stream">Readable, seekable stream disposed when closed or replaced.</param>
    /// <param name="sourceName">Display name for the log.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The indexed ready state.</returns>
    Task<ReplaySessionSnapshot> LoadAsync(
        Stream stream,
        string sourceName,
        CancellationToken cancellationToken = default);

    /// <summary>Starts or resumes speed-adjusted playback without blocking until completion.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The playing state.</returns>
    Task<ReplaySessionSnapshot> PlayAsync(CancellationToken cancellationToken = default);

    /// <summary>Pauses playback at the next cancellation boundary.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The paused state.</returns>
    Task<ReplaySessionSnapshot> PauseAsync(CancellationToken cancellationToken = default);

    /// <summary>Seeks to recorded elapsed time and reconstructs replay-only vehicle state.</summary>
    /// <param name="position">Elapsed recorded time from the first frame.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The paused state at the selected position.</returns>
    Task<ReplaySessionSnapshot> SeekAsync(
        TimeSpan position,
        CancellationToken cancellationToken = default);

    /// <summary>Changes playback speed for subsequent replay intervals.</summary>
    /// <param name="speed">Speed multiplier from 0.1 through 50.</param>
    /// <returns>The updated replay state.</returns>
    ReplaySessionSnapshot SetSpeed(double speed);

    /// <summary>Closes the replay, disposes its stream, and re-enables transmission.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task CloseAsync(CancellationToken cancellationToken = default);
}
