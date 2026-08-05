using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Simulation;

namespace MissionPlanner.Core.Simulation;

/// <summary>Contains the immutable observable state of one simulation session.</summary>
/// <param name="SessionId">MissionPlanner-owned session identity.</param>
/// <param name="Profile">Profile used to start the session.</param>
/// <param name="State">Lifecycle state.</param>
/// <param name="RuntimeIdentity">Exact runtime identity after creation.</param>
/// <param name="ConnectionEndpoints">Endpoints reported by the runtime.</param>
/// <param name="StartedAt">Runtime start timestamp.</param>
/// <param name="EndedAt">Terminal timestamp.</param>
/// <param name="Message">Current user-facing status.</param>
/// <param name="Failure">Failure detail, when applicable.</param>
/// <param name="RecentOutput">Bounded recent output.</param>
/// <param name="VehicleId">Verified connected simulator vehicle identity.</param>
/// <param name="Artifacts">Isolated artifact paths assigned to the instance.</param>
/// <param name="RuntimeDiagnostics">Runtime command, version, process, and heartbeat diagnostics.</param>
public sealed record SimulationSessionSnapshot(
    Guid SessionId,
    SimulatorProfile? Profile,
    SimulationSessionState State,
    SimulatorRuntimeIdentity? RuntimeIdentity,
    IReadOnlyList<SimulationEndpoint> ConnectionEndpoints,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt,
    string Message,
    string? Failure,
    IReadOnlyList<SimulatorOutputLine> RecentOutput,
    VehicleId? VehicleId = null,
    SimulationInstanceArtifacts? Artifacts = null,
    SimulationRuntimeDiagnostics? RuntimeDiagnostics = null)
{
    /// <summary>Creates the initial stopped workspace state.</summary>
    public static SimulationSessionSnapshot Stopped { get; } = new(
        Guid.Empty,
        null,
        SimulationSessionState.Stopped,
        null,
        [],
        null,
        null,
        "No simulation is running.",
        null,
        []);
}
