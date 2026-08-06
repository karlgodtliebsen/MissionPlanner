namespace MissionPlanner.Simulation.Abstractions;

/// <summary>Coordinates one observable simulation session.</summary>
public interface ISimulationSessionManager : IAsyncDisposable
{
    /// <summary>Gets the current immutable session snapshot.</summary>
    SimulationSessionSnapshot Current { get; }

    /// <summary>Occurs after the current simulation state changes.</summary>
    event EventHandler<SimulationSessionChangedEventArgs>? Changed;

    /// <summary>Validates and starts a simulator profile.</summary>
    /// <param name="profile">The selected profile.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting state.</returns>
    Task<SimulationSessionSnapshot> StartAsync(
        SimulatorProfile profile,
        CancellationToken cancellationToken = default);

    /// <summary>Stops the exact currently owned runtime session.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting state.</returns>
    Task<SimulationSessionSnapshot> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops and starts the last selected profile.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting state.</returns>
    Task<SimulationSessionSnapshot> RestartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops all runtime resources owned by the workspace during application shutdown.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
