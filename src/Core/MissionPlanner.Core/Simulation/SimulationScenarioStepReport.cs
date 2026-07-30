namespace MissionPlanner.Core.Simulation;

/// <summary>Records one scenario step's timing, result, evidence, and telemetry.</summary>
/// <param name="StepId">Step identifier.</param>
/// <param name="Name">Readable step name.</param>
/// <param name="Kind">Step kind.</param>
/// <param name="StartedAt">Step start time.</param>
/// <param name="EndedAt">Step end time.</param>
/// <param name="Result">Step result.</param>
/// <param name="Evidence">Acknowledgement, condition, or failure evidence.</param>
/// <param name="Telemetry">Telemetry captured at completion.</param>
public sealed record SimulationScenarioStepReport(
    string StepId,
    string Name,
    SimulationScenarioStepKind Kind,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    SimulationScenarioStepResult Result,
    string Evidence,
    SimulationTelemetrySnapshot? Telemetry);
