namespace MissionPlanner.Simulation;

/// <summary>Provides simulation state-change event data.</summary>
/// <param name="snapshot">The new immutable state.</param>
public sealed class SimulationSessionChangedEventArgs(SimulationSessionSnapshot snapshot) : EventArgs
{
    /// <summary>Gets the new simulation state.</summary>
    public SimulationSessionSnapshot Snapshot { get; } = snapshot;
}
