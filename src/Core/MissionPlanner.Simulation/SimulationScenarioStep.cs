namespace MissionPlanner.Simulation;

/// <summary>Defines one declarative, bounded scenario step.</summary>
/// <param name="Id">Unique stable step identifier.</param>
/// <param name="Kind">Step operation.</param>
/// <param name="Name">Readable step name.</param>
/// <param name="TimeoutSeconds">Explicit per-step timeout.</param>
/// <param name="State">Required named state for <see cref="SimulationScenarioStepKind.WaitForState"/>.</param>
/// <param name="Mode">Firmware mode name for <see cref="SimulationScenarioStepKind.SetMode"/>.</param>
/// <param name="Value">Typed takeoff altitude or simulation-control value.</param>
/// <param name="Condition">Typed wait/assert condition.</param>
/// <param name="ControlKey">Documented simulation-control key.</param>
/// <param name="DurationSeconds">Bounded injected-control duration.</param>
/// <param name="MissionItems">Embedded typed mission items.</param>
public sealed record SimulationScenarioStep(
    string Id,
    SimulationScenarioStepKind Kind,
    string Name,
    int TimeoutSeconds,
    SimulationVehicleStateRequirement? State = null,
    string? Mode = null,
    SimulationScenarioValue? Value = null,
    SimulationTelemetryCondition? Condition = null,
    string? ControlKey = null,
    int? DurationSeconds = null,
    IReadOnlyList<SimulationScenarioMissionItem>? MissionItems = null);
