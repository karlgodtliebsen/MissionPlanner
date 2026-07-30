namespace MissionPlanner.Core.Simulation;

/// <summary>Validates simulator profiles and host resources before runtime creation.</summary>
public interface ISimulatorProfileValidator
{
    /// <summary>Validates a profile for launch.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>All detected validation issues.</returns>
    ValueTask<IReadOnlyList<SimulationValidationIssue>> ValidateAsync(
        SimulatorProfile profile,
        CancellationToken cancellationToken = default);
}
