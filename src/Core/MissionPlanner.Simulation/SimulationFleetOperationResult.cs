namespace MissionPlanner.Simulation;

/// <summary>Contains one member's start or stop result.</summary>
/// <param name="FleetSessionId">Exact fleet member identity.</param>
/// <param name="Succeeded">Whether the requested terminal state was reached.</param>
/// <param name="Session">Resulting session snapshot.</param>
/// <param name="Error">Per-session failure detail.</param>
public sealed record SimulationFleetOperationResult(
    Guid FleetSessionId,
    bool Succeeded,
    SimulationSessionSnapshot Session,
    string? Error);
