namespace MissionPlanner.Core.Simulation;

/// <summary>Provides scenario-runner state-change event data.</summary>
/// <param name="snapshot">New runner state.</param>
public sealed class SimulationScenarioRunnerChangedEventArgs(SimulationScenarioRunnerSnapshot snapshot) : EventArgs
{
    /// <summary>Gets the new runner state.</summary>
    public SimulationScenarioRunnerSnapshot Snapshot { get; } = snapshot;
}
