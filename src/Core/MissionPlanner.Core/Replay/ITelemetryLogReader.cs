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
