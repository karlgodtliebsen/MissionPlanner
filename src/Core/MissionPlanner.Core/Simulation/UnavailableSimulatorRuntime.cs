namespace MissionPlanner.Core.Simulation;

/// <summary>Explicitly reports that a launch adapter is not installed yet.</summary>
public sealed class UnavailableSimulatorRuntime : ISimulatorRuntime
{
    /// <inheritdoc />
    public string Name => "Unavailable";

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<SimulationValidationIssue>> ValidateAsync(
        SimulatorProfile profile,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<SimulationValidationIssue> result =
        [
            new SimulationValidationIssue(
                "runtime.unavailable",
                "runtime",
                "No simulator launch runtime is installed. ArduPilot SITL runtime support is provided by Simulation step 03.")
        ];
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc />
    public Task<ISimulatorRuntimeSession> StartAsync(
        SimulatorStartRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("No simulator launch runtime is installed.");
}
