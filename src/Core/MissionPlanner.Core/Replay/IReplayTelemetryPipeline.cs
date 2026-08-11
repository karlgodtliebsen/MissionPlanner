namespace MissionPlanner.Core.Replay;

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
