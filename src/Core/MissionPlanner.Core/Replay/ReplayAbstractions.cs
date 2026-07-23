namespace MissionPlanner.Core.Replay;

/// <summary>Indexes and reads timestamp-prefixed MAVLink telemetry-log packets.</summary>
public interface ITelemetryLogReader
{
    /// <summary>Builds a random-access index without retaining packet payloads in memory.</summary>
    /// <param name="stream">Readable, seekable telemetry-log stream.</param>
    /// <param name="sourceName">Caller-provided display name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The complete structural index.</returns>
    Task<TelemetryLogIndex> IndexAsync(
        Stream stream,
        string sourceName,
        CancellationToken cancellationToken = default);

    /// <summary>Reads one packet using a previously created index entry.</summary>
    /// <param name="stream">The same readable, seekable telemetry-log stream.</param>
    /// <param name="entry">Indexed packet entry.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The packet and index metadata.</returns>
    Task<TelemetryLogRecord> ReadAsync(
        Stream stream,
        TelemetryLogIndexEntry entry,
        CancellationToken cancellationToken = default);
}

/// <summary>Projects decoded replay messages into a registry isolated from live vehicles.</summary>
public interface IReplayTelemetryPipeline
{
    /// <summary>Gets immutable vehicle states produced only by replay traffic.</summary>
    IReadOnlyList<MissionPlanner.Core.Vehicles.Models.VehicleState> Vehicles { get; }

    /// <summary>Clears every replay-only vehicle and parser state.</summary>
    void Reset();

    /// <summary>Parses, decodes, and dispatches one complete replay frame.</summary>
    /// <param name="packet">Complete MAVLink frame bytes.</param>
    /// <param name="receivedAt">Recorded frame timestamp.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the frame was decoded.</returns>
    ValueTask<bool> ProcessAsync(
        ReadOnlyMemory<byte> packet,
        DateTimeOffset receivedAt,
        CancellationToken cancellationToken = default);
}

/// <summary>Abstracts replay timing so deterministic tests never wait on wall-clock time.</summary>
public interface IReplayDelay
{
    /// <summary>Waits for one speed-adjusted interval.</summary>
    /// <param name="delay">Non-negative wall-clock delay.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

/// <summary>Exposes the current deterministic replay clock.</summary>
public interface IReplayClock
{
    /// <summary>Gets the replay clock, or <see langword="null"/> when no log is loaded.</summary>
    ReplayClockSnapshot? Current { get; }
}

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
