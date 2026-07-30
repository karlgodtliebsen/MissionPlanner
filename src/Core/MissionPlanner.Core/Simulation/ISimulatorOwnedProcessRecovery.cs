namespace MissionPlanner.Core.Simulation;

/// <summary>Performs platform-specific exact-process identity verification and recovery.</summary>
public interface ISimulatorOwnedProcessRecovery
{
    /// <summary>Recovers a process only when PID, executable path, and start time all match.</summary>
    /// <param name="ownedProcess">Persisted process identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The recovery result.</returns>
    Task<SimulationOrphanRecoveryResult> RecoverAsync(
        SimulationOwnedProcess ownedProcess,
        CancellationToken cancellationToken = default);
}
