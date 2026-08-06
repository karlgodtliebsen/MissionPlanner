namespace MissionPlanner.Simulation.Abstractions;

/// <summary>Exports scenario reports without changing their evidence.</summary>
public interface ISimulationScenarioReportExporter
{
    /// <summary>Exports versioned machine-readable JSON.</summary>
    /// <param name="report">Run report.</param>
    /// <returns>Indented JSON.</returns>
    string ToJson(SimulationScenarioRunReport report);

    /// <summary>Exports a concise human-readable report.</summary>
    /// <param name="report">Run report.</param>
    /// <returns>Plain text.</returns>
    string ToText(SimulationScenarioRunReport report);
}
