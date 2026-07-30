namespace MissionPlanner.Core.Simulation;

/// <summary>Provides observable scenario runner state.</summary>
/// <param name="State">Runner state.</param>
/// <param name="RunId">Active or last run identity.</param>
/// <param name="StepId">Current or last step identity.</param>
/// <param name="Message">Readable state detail.</param>
public sealed record SimulationScenarioRunnerSnapshot(
    SimulationScenarioRunnerState State,
    Guid? RunId,
    string? StepId,
    string Message)
{
    /// <summary>Gets the initial idle state.</summary>
    public static SimulationScenarioRunnerSnapshot Idle { get; } =
        new(SimulationScenarioRunnerState.Idle, null, null, "No scenario is running.");
}
