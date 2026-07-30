namespace MissionPlanner.Core.Simulation;

/// <summary>Starts an exact local process without exposing process APIs to Core.</summary>
public interface ISimulatorProcessHost
{
    /// <summary>Starts a local process from tokenized settings.</summary>
    /// <param name="startInfo">Typed process settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The exactly owned process session.</returns>
    Task<ISimulatorProcessSession> StartAsync(
        SimulatorProcessStartInfo startInfo,
        CancellationToken cancellationToken = default);
}
