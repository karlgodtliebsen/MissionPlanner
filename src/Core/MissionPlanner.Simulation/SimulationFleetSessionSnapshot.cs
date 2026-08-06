using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Simulation;

/// <summary>Describes one observable member of the simulation fleet.</summary>
/// <param name="Allocation">Deterministic member allocation.</param>
/// <param name="Session">Current runtime session state.</param>
/// <param name="IsSelected">Whether this member is the active workspace selection.</param>
public sealed record SimulationFleetSessionSnapshot(
    SimulationInstanceAllocation Allocation,
    SimulationSessionSnapshot Session,
    bool IsSelected)
{
    /// <summary>Gets the exact connected vehicle target when the member is ready.</summary>
    public VehicleId? VehicleId => Session.VehicleId;
}
