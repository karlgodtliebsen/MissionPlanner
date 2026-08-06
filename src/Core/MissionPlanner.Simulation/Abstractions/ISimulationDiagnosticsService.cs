namespace MissionPlanner.Simulation.Abstractions;

/// <summary>Builds a non-secret diagnostic document for a simulation session.</summary>
public interface ISimulationDiagnosticsService
{
    /// <summary>Creates a structured diagnostic bundle.</summary>
    /// <param name="snapshot">The session snapshot.</param>
    /// <returns>A redacted JSON document.</returns>
    string CreateBundle(SimulationSessionSnapshot snapshot);
}
