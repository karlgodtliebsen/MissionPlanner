namespace MissionPlanner.Core.Simulation;

/// <summary>Reserves an endpoint set against other MissionPlanner-owned simulator sessions.</summary>
public interface ISimulationPortAllocator
{
    /// <summary>Reserves all profile endpoints as one atomic lease.</summary>
    /// <param name="endpoints">Endpoints to reserve.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An exact lease released when the runtime ends.</returns>
    ValueTask<ISimulationPortLease> ReserveAsync(
        IReadOnlyList<SimulationEndpoint> endpoints,
        CancellationToken cancellationToken = default);
}
