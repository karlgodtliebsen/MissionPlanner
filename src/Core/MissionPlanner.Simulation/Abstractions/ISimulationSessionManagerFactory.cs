namespace MissionPlanner.Simulation.Abstractions;

/// <summary>Creates independent single-session coordinators for fleet members.</summary>
public interface ISimulationSessionManagerFactory
{
    /// <summary>Creates an independent session manager.</summary>
    /// <returns>The new session manager.</returns>
    ISimulationSessionManager Create();
}
