namespace MissionPlanner.Core.Simulation;

/// <summary>Persists and recovers exact process ownership markers.</summary>
public interface ISimulationOwnershipStore
{
    /// <summary>Marks an exact process as owned by the current application lifetime.</summary>
    /// <param name="ownedProcess">Owned process identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task MarkAsync(SimulationOwnedProcess ownedProcess, CancellationToken cancellationToken = default);

    /// <summary>Releases one exact ownership marker after cleanup.</summary>
    /// <param name="sessionId">Owning session identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ReleaseAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Recovers persisted markers that are not active in this application lifetime.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>All attempted recovery results.</returns>
    Task<IReadOnlyList<SimulationOrphanRecoveryResult>> RecoverOrphansAsync(
        CancellationToken cancellationToken = default);
}
