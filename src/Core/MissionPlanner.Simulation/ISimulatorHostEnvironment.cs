namespace MissionPlanner.Core.Simulation;

/// <summary>Provides host-specific validation without coupling the workspace to a runtime implementation.</summary>
public interface ISimulatorHostEnvironment
{
    /// <summary>Validates whether an executable path exists and is executable on this host.</summary>
    /// <param name="executablePath">Absolute executable path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A validation issue, or <see langword="null"/> when valid.</returns>
    ValueTask<SimulationValidationIssue?> ValidateExecutableAsync(
        string executablePath,
        CancellationToken cancellationToken = default);

    /// <summary>Checks whether a requested endpoint port is currently available.</summary>
    /// <param name="endpoint">The requested endpoint.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the port is currently available.</returns>
    ValueTask<bool> IsPortAvailableAsync(
        SimulationEndpoint endpoint,
        CancellationToken cancellationToken = default);
}
