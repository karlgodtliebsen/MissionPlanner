using MissionPlanner.Simulation.Abstractions;

namespace MissionPlanner.Simulation;

/// <summary>Declines orphan recovery on hosts without an exact process-identity implementation.</summary>
public sealed class UnavailableSimulatorOwnedProcessRecovery : ISimulatorOwnedProcessRecovery
{
    /// <inheritdoc />
    public Task<SimulationOrphanRecoveryResult> RecoverAsync(
        SimulationOwnedProcess ownedProcess,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SimulationOrphanRecoveryResult(
            ownedProcess,
            SimulationOrphanRecoveryState.Failed,
            "Safe local process recovery is unavailable on this host."));
    }
}
