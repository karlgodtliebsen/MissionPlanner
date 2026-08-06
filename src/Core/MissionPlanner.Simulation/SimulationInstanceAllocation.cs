namespace MissionPlanner.Simulation;

/// <summary>Contains all resources deterministically allocated to one fleet member.</summary>
/// <param name="FleetSessionId">Stable identity derived from the base profile and member index.</param>
/// <param name="Index">Zero-based fleet member index.</param>
/// <param name="Profile">Fully allocated launch profile.</param>
/// <param name="Offset">Applied launch offset.</param>
/// <param name="Artifacts">Isolated artifact paths.</param>
public sealed record SimulationInstanceAllocation(
    Guid FleetSessionId,
    int Index,
    SimulatorProfile Profile,
    SimulationFormationOffset Offset,
    SimulationInstanceArtifacts Artifacts);
