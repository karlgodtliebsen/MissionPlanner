using MissionPlanner.Core.Simulation;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Simulation;

/// <summary>Contains a complete machine-readable scenario run report.</summary>
/// <param name="ReportVersion">Report schema version.</param>
/// <param name="RunId">Unique run identity.</param>
/// <param name="ScenarioId">Scenario identity.</param>
/// <param name="ScenarioName">Scenario name.</param>
/// <param name="SessionId">Exact simulation session.</param>
/// <param name="VehicleId">Exact target vehicle.</param>
/// <param name="StartedAt">Run start time.</param>
/// <param name="EndedAt">Run end time.</param>
/// <param name="Result">Overall result.</param>
/// <param name="DryRun">Whether no vehicle-changing operation was executed.</param>
/// <param name="Summary">Readable result summary.</param>
/// <param name="Validation">Schema and capability evidence.</param>
/// <param name="Steps">Ordered step reports.</param>
/// <param name="FinalTelemetry">Final target telemetry.</param>
public sealed record SimulationScenarioRunReport(
    int ReportVersion,
    Guid RunId,
    Guid ScenarioId,
    string ScenarioName,
    Guid SessionId,
    VehicleId VehicleId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    SimulationScenarioRunResult Result,
    bool DryRun,
    string Summary,
    SimulationScenarioValidationReport Validation,
    IReadOnlyList<SimulationScenarioStepReport> Steps,
    SimulationTelemetrySnapshot? FinalTelemetry);
