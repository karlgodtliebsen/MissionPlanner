namespace MissionPlanner.Core.Simulation;

/// <summary>Contains dry-run schema and live-capability results.</summary>
/// <param name="Issues">Validation issues.</param>
/// <param name="Capabilities">Required live capabilities.</param>
public sealed record SimulationScenarioValidationReport(IReadOnlyList<SimulationScenarioValidationIssue> Issues, IReadOnlyList<SimulationScenarioCapability> Capabilities)
{
    /// <summary>Gets whether no validation errors or unavailable required capabilities exist.</summary>
    public bool IsValid =>
        Issues.All(item => item.Severity != SimulationScenarioValidationSeverity.Error) &&
        Capabilities.All(item => item.Available);
}
