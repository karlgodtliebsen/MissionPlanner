namespace MissionPlanner.Core.Simulation;

/// <summary>Allocates collision-free, deterministic resources for a simulator fleet.</summary>
public interface ISimulationFleetAllocator
{
    /// <summary>Allocates all requested instances atomically.</summary>
    /// <param name="request">Fleet launch request.</param>
    /// <param name="occupied">Currently occupied allocations.</param>
    /// <returns>The ordered allocation set.</returns>
    IReadOnlyList<SimulationInstanceAllocation> Allocate(
        SimulationFleetLaunchRequest request,
        IReadOnlyCollection<SimulationInstanceAllocation> occupied);
}
