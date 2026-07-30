namespace MissionPlanner.Core.Simulation;

/// <summary>Defines a process-, container-, or remote-neutral simulator runtime adapter.</summary>
public interface ISimulatorRuntime
{
    /// <summary>Gets the runtime adapter name.</summary>
    string Name { get; }

    /// <summary>Performs runtime-specific validation without starting a session.</summary>
    /// <param name="profile">The structurally valid profile.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Runtime-specific validation issues.</returns>
    ValueTask<IReadOnlyList<SimulationValidationIssue>> ValidateAsync(
        SimulatorProfile profile,
        CancellationToken cancellationToken = default);

    /// <summary>Starts and returns one exactly identified owned runtime session.</summary>
    /// <param name="request">The typed start request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The owned runtime session.</returns>
    Task<ISimulatorRuntimeSession> StartAsync(
        SimulatorStartRequest request,
        CancellationToken cancellationToken = default);
}
