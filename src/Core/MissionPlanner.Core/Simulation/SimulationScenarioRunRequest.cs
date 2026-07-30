using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Simulation;

/// <summary>Defines one run request bound to an exact simulation target.</summary>
/// <param name="Document">Validated declarative document.</param>
/// <param name="SessionId">Exact simulation session ID.</param>
/// <param name="VehicleId">Exact verified vehicle ID.</param>
/// <param name="DryRun">Whether to validate without executing.</param>
/// <param name="HazardousActionsConfirmed">Explicit confirmation for arm, takeoff, mission start, and fault steps.</param>
public sealed record SimulationScenarioRunRequest(
    SimulationScenarioDocument Document,
    Guid SessionId,
    VehicleId VehicleId,
    bool DryRun,
    bool HazardousActionsConfirmed);
