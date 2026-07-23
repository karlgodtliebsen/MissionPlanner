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

/// <summary>Creates independent single-session coordinators for fleet members.</summary>
public interface ISimulationSessionManagerFactory
{
    /// <summary>Creates an independent session manager.</summary>
    /// <returns>The new session manager.</returns>
    ISimulationSessionManager Create();
}

/// <summary>Coordinates independently owned simulator sessions without a global current-vehicle assumption.</summary>
public interface ISimulationFleetManager : IAsyncDisposable
{
    /// <summary>Gets all fleet sessions.</summary>
    IReadOnlyList<SimulationFleetSessionSnapshot> Sessions { get; }

    /// <summary>Gets the explicitly selected fleet member identity.</summary>
    Guid? SelectedSessionId { get; }

    /// <summary>Occurs after one member or the active selection changes.</summary>
    event EventHandler<SimulationFleetChangedEventArgs>? Changed;

    /// <summary>Allocates and starts all requested members with bounded concurrency.</summary>
    /// <param name="request">Fleet launch request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Per-session start results.</returns>
    Task<SimulationFleetOperationReport> StartAllAsync(
        SimulationFleetLaunchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Stops all owned members with bounded concurrency.</summary>
    /// <param name="maximumConcurrency">Maximum concurrent stop operations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Per-session stop results.</returns>
    Task<SimulationFleetOperationReport> StopAllAsync(
        int maximumConcurrency = 3,
        CancellationToken cancellationToken = default);

    /// <summary>Stops one exact fleet member.</summary>
    /// <param name="fleetSessionId">Fleet member identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting member state.</returns>
    Task<SimulationFleetOperationResult> StopAsync(
        Guid fleetSessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Changes active selection without changing any vehicle or runtime state.</summary>
    /// <param name="fleetSessionId">Fleet member identity.</param>
    void Select(Guid fleetSessionId);

    /// <summary>Resolves an exact runnable session and vehicle target.</summary>
    /// <param name="fleetSessionId">Fleet member identity.</param>
    /// <returns>The session snapshot with an exact connected vehicle.</returns>
    SimulationFleetSessionSnapshot GetRunnableTarget(Guid fleetSessionId);
}
