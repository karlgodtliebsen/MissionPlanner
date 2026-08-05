namespace MissionPlanner.Simulation.Abstractions;

/// <summary>Parses and validates the safe declarative scenario schema.</summary>
public interface ISimulationScenarioParser
{
    /// <summary>Parses a JSON scenario and rejects unknown fields or unsupported schema versions.</summary>
    /// <param name="json">Scenario JSON.</param>
    /// <returns>The parsed document.</returns>
    SimulationScenarioDocument Parse(string json);

    /// <summary>Validates schema structure without accessing a live vehicle.</summary>
    /// <param name="document">Scenario document.</param>
    /// <returns>All schema issues.</returns>
    IReadOnlyList<SimulationScenarioValidationIssue> Validate(SimulationScenarioDocument document);

    /// <summary>Serializes a scenario using the current safe schema.</summary>
    /// <param name="document">Scenario document.</param>
    /// <returns>Indented JSON.</returns>
    string Serialize(SimulationScenarioDocument document);
}
