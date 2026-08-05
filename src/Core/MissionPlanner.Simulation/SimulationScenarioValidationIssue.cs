namespace MissionPlanner.Simulation;

/// <summary>Describes one scenario validation problem.</summary>
/// <param name="Severity">Problem severity.</param>
/// <param name="Path">Schema or capability path.</param>
/// <param name="Message">Readable problem detail.</param>
public sealed record SimulationScenarioValidationIssue(SimulationScenarioValidationSeverity Severity, string Path, string Message);
