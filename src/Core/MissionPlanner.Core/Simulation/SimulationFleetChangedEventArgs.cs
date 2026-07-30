namespace MissionPlanner.Core.Simulation;

/// <summary>Provides fleet state-change data.</summary>
/// <param name="session">The member that changed.</param>
public sealed class SimulationFleetChangedEventArgs(SimulationFleetSessionSnapshot session) : EventArgs
{
    /// <summary>Gets the changed fleet member snapshot.</summary>
    public SimulationFleetSessionSnapshot Session { get; } = session;
}
