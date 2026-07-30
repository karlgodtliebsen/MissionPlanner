namespace MissionPlanner.Core.Simulation;

/// <summary>Loads and persists simulator profiles.</summary>
public interface ISimulatorProfileService
{
    /// <summary>Gets the initialized profile collection.</summary>
    IReadOnlyList<SimulatorProfile> Profiles { get; }

    /// <summary>Loads persisted profiles, recovering to a safe default when necessary.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The initialized profiles.</returns>
    ValueTask<IReadOnlyList<SimulatorProfile>> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds or replaces a profile and persists the complete collection.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask SaveAsync(SimulatorProfile profile, CancellationToken cancellationToken = default);

    /// <summary>Deletes a profile and ensures at least one default remains.</summary>
    /// <param name="profileId">The profile identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask DeleteAsync(Guid profileId, CancellationToken cancellationToken = default);
}
