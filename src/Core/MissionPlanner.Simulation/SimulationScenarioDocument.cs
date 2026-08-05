namespace MissionPlanner.Core.Simulation;

/// <summary>Defines a schema-versioned declarative simulation scenario.</summary>
/// <param name="SchemaVersion">Scenario schema version; currently 1.</param>
/// <param name="Id">Stable scenario identity.</param>
/// <param name="Name">Readable scenario name.</param>
/// <param name="Variables">Safe typed values available by exact name.</param>
/// <param name="Steps">Ordered bounded steps.</param>
public sealed record SimulationScenarioDocument(
    int SchemaVersion,
    Guid Id,
    string Name,
    IReadOnlyDictionary<string, SimulationScenarioValue> Variables,
    IReadOnlyList<SimulationScenarioStep> Steps);
