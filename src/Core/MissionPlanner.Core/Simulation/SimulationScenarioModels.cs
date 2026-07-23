using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Simulation;

/// <summary>Identifies a declarative simulation scenario step.</summary>
public enum SimulationScenarioStepKind
{
    /// <summary>Waits for a named connection or flight state.</summary>
    WaitForState,
    /// <summary>Changes to a named firmware-supported mode.</summary>
    SetMode,
    /// <summary>Arms the selected vehicle.</summary>
    Arm,
    /// <summary>Starts a confirmed takeoff.</summary>
    Takeoff,
    /// <summary>Uploads embedded, typed MAVLink mission items.</summary>
    UploadMission,
    /// <summary>Starts the uploaded mission through an acknowledged MAVLink command.</summary>
    StartMission,
    /// <summary>Waits for a typed telemetry condition.</summary>
    WaitCondition,
    /// <summary>Applies a documented bounded simulation control.</summary>
    InjectFault,
    /// <summary>Resets a previously injected simulation control.</summary>
    ClearFault,
    /// <summary>Commands the selected vehicle to land.</summary>
    Land,
    /// <summary>Waits for and records a required telemetry assertion.</summary>
    AssertTelemetry
}

/// <summary>Identifies a named state used by a wait-for-state step.</summary>
public enum SimulationVehicleStateRequirement
{
    /// <summary>The exact vehicle is online.</summary>
    Online,
    /// <summary>The exact vehicle is armed.</summary>
    Armed,
    /// <summary>The exact vehicle is disarmed.</summary>
    Disarmed,
    /// <summary>The vehicle reports that it is on the ground.</summary>
    OnGround,
    /// <summary>The vehicle reports that it is in the air.</summary>
    InAir
}

/// <summary>Identifies a safe scenario value type.</summary>
public enum SimulationScenarioValueKind
{
    /// <summary>A Boolean value.</summary>
    Boolean,
    /// <summary>A finite numeric value.</summary>
    Number,
    /// <summary>A bounded text value.</summary>
    Text
}

/// <summary>Identifies telemetry exposed to declarative conditions.</summary>
public enum SimulationTelemetryMetric
{
    /// <summary>Whether the vehicle connection is online.</summary>
    Online,
    /// <summary>Whether the vehicle is armed.</summary>
    Armed,
    /// <summary>The semantic flight mode name.</summary>
    Mode,
    /// <summary>The extended landed-state name.</summary>
    LandedState,
    /// <summary>The primary GPS fix-type name.</summary>
    GpsFixType,
    /// <summary>Relative altitude in metres.</summary>
    RelativeAltitudeMeters,
    /// <summary>Mean-sea-level altitude in metres.</summary>
    AltitudeMslMeters,
    /// <summary>Ground speed in metres per second.</summary>
    GroundSpeedMetersPerSecond,
    /// <summary>Remaining battery percentage.</summary>
    BatteryRemainingPercent,
    /// <summary>Latitude in decimal degrees.</summary>
    LatitudeDegrees,
    /// <summary>Longitude in decimal degrees.</summary>
    LongitudeDegrees
}

/// <summary>Identifies a supported, side-effect-free telemetry comparison.</summary>
public enum SimulationComparisonOperator
{
    /// <summary>Values are equal, within numeric tolerance where applicable.</summary>
    Equal,
    /// <summary>Values are not equal.</summary>
    NotEqual,
    /// <summary>The observed number is greater than the expected number.</summary>
    GreaterThan,
    /// <summary>The observed number is greater than or equal to the expected number.</summary>
    GreaterThanOrEqual,
    /// <summary>The observed number is less than the expected number.</summary>
    LessThan,
    /// <summary>The observed number is less than or equal to the expected number.</summary>
    LessThanOrEqual
}

/// <summary>Identifies scenario validation severity.</summary>
public enum SimulationScenarioValidationSeverity
{
    /// <summary>Execution cannot proceed.</summary>
    Error,
    /// <summary>Execution can proceed with an explicit limitation.</summary>
    Warning
}

/// <summary>Identifies overall scenario execution result.</summary>
public enum SimulationScenarioRunResult
{
    /// <summary>All steps completed successfully.</summary>
    Succeeded,
    /// <summary>Dry-run validation completed without vehicle-changing actions.</summary>
    DryRun,
    /// <summary>Validation or execution failed.</summary>
    Failed,
    /// <summary>The caller canceled the run.</summary>
    Canceled
}

/// <summary>Identifies one step execution result.</summary>
public enum SimulationScenarioStepResult
{
    /// <summary>The step was validated but not executed during a dry run.</summary>
    Planned,
    /// <summary>The step completed successfully.</summary>
    Succeeded,
    /// <summary>The step failed or timed out.</summary>
    Failed,
    /// <summary>The step was canceled.</summary>
    Canceled
}

/// <summary>Identifies observable scenario-runner state.</summary>
public enum SimulationScenarioRunnerState
{
    /// <summary>No run is active.</summary>
    Idle,
    /// <summary>The scenario and live capabilities are being validated.</summary>
    Validating,
    /// <summary>A step is executing.</summary>
    Running,
    /// <summary>A pause will occur after the current step reaches a safe boundary.</summary>
    PauseRequested,
    /// <summary>Execution is paused between steps.</summary>
    Paused,
    /// <summary>The run completed successfully.</summary>
    Completed,
    /// <summary>The run failed.</summary>
    Failed,
    /// <summary>The run was canceled.</summary>
    Canceled
}

/// <summary>Stores a literal safe value or a reference to a declared variable.</summary>
/// <param name="Kind">Expected value type.</param>
/// <param name="BooleanValue">Boolean literal.</param>
/// <param name="NumberValue">Finite numeric literal.</param>
/// <param name="TextValue">Bounded text literal.</param>
/// <param name="Variable">Declared variable name, without expression syntax.</param>
public sealed record SimulationScenarioValue(
    SimulationScenarioValueKind Kind,
    bool? BooleanValue = null,
    double? NumberValue = null,
    string? TextValue = null,
    string? Variable = null);

/// <summary>Defines one typed, side-effect-free telemetry condition.</summary>
/// <param name="Metric">Telemetry metric.</param>
/// <param name="Operator">Comparison operator.</param>
/// <param name="Expected">Literal or declared variable value.</param>
/// <param name="Tolerance">Optional non-negative tolerance for numeric equality.</param>
public sealed record SimulationTelemetryCondition(
    SimulationTelemetryMetric Metric,
    SimulationComparisonOperator Operator,
    SimulationScenarioValue Expected,
    double? Tolerance = null);

/// <summary>Defines one safe embedded mission item for scenario upload.</summary>
/// <param name="Frame">MAV_FRAME numeric value.</param>
/// <param name="Command">MAV_CMD numeric value.</param>
/// <param name="Current">Whether the item is the current mission item.</param>
/// <param name="AutoContinue">Whether execution automatically advances.</param>
/// <param name="Param1">Command parameter 1.</param>
/// <param name="Param2">Command parameter 2.</param>
/// <param name="Param3">Command parameter 3.</param>
/// <param name="Param4">Command parameter 4.</param>
/// <param name="X">Protocol X coordinate/value.</param>
/// <param name="Y">Protocol Y coordinate/value.</param>
/// <param name="Z">Protocol Z coordinate/value.</param>
public sealed record SimulationScenarioMissionItem(
    byte Frame,
    ushort Command,
    bool Current,
    bool AutoContinue,
    float Param1,
    float Param2,
    float Param3,
    float Param4,
    int X,
    int Y,
    float Z);

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

/// <summary>Describes one scenario validation problem.</summary>
/// <param name="Severity">Problem severity.</param>
/// <param name="Path">Schema or capability path.</param>
/// <param name="Message">Readable problem detail.</param>
public sealed record SimulationScenarioValidationIssue(
    SimulationScenarioValidationSeverity Severity,
    string Path,
    string Message);

/// <summary>Describes one live capability required by a scenario.</summary>
/// <param name="Name">Capability name.</param>
/// <param name="Available">Whether the exact target supports it.</param>
/// <param name="Reason">Availability evidence.</param>
public sealed record SimulationScenarioCapability(string Name, bool Available, string Reason);

/// <summary>Contains dry-run schema and live-capability results.</summary>
/// <param name="Issues">Validation issues.</param>
/// <param name="Capabilities">Required live capabilities.</param>
public sealed record SimulationScenarioValidationReport(
    IReadOnlyList<SimulationScenarioValidationIssue> Issues,
    IReadOnlyList<SimulationScenarioCapability> Capabilities)
{
    /// <summary>Gets whether no validation errors or unavailable required capabilities exist.</summary>
    public bool IsValid =>
        Issues.All(item => item.Severity != SimulationScenarioValidationSeverity.Error) &&
        Capabilities.All(item => item.Available);
}

/// <summary>Captures auditable telemetry evidence at a step boundary.</summary>
/// <param name="ObservedAt">Snapshot time.</param>
/// <param name="ConnectionState">Vehicle connection state.</param>
/// <param name="Mode">Semantic mode.</param>
/// <param name="Armed">Whether the vehicle is armed.</param>
/// <param name="LandedState">Extended landed state.</param>
/// <param name="LatitudeDegrees">Latitude.</param>
/// <param name="LongitudeDegrees">Longitude.</param>
/// <param name="AltitudeMslMeters">Mean-sea-level altitude.</param>
/// <param name="RelativeAltitudeMeters">Relative altitude.</param>
/// <param name="GroundSpeedMetersPerSecond">Ground speed.</param>
/// <param name="BatteryRemainingPercent">Battery percentage.</param>
/// <param name="GpsFixType">Primary GPS fix type.</param>
public sealed record SimulationTelemetrySnapshot(
    DateTimeOffset ObservedAt,
    VehicleConnectionState ConnectionState,
    VehicleMode Mode,
    bool Armed,
    VehicleLandedState LandedState,
    double? LatitudeDegrees,
    double? LongitudeDegrees,
    double? AltitudeMslMeters,
    double? RelativeAltitudeMeters,
    double? GroundSpeedMetersPerSecond,
    int? BatteryRemainingPercent,
    GpsFixType GpsFixType = GpsFixType.Unknown);

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

/// <summary>Defines one run request bound to an exact simulation target.</summary>
/// <param name="Document">Validated declarative document.</param>
/// <param name="SessionId">Exact simulation session ID.</param>
/// <param name="VehicleId">Exact verified vehicle ID.</param>
/// <param name="DryRun">Whether to validate without executing.</param>
/// <param name="HazardousActionsConfirmed">Explicit confirmation for arm, takeoff, mission start, and fault steps.</param>
public sealed record SimulationScenarioRunRequest(
    SimulationScenarioDocument Document,
    Guid SessionId,
    VehicleId VehicleId,
    bool DryRun,
    bool HazardousActionsConfirmed);

/// <summary>Provides observable scenario runner state.</summary>
/// <param name="State">Runner state.</param>
/// <param name="RunId">Active or last run identity.</param>
/// <param name="StepId">Current or last step identity.</param>
/// <param name="Message">Readable state detail.</param>
public sealed record SimulationScenarioRunnerSnapshot(
    SimulationScenarioRunnerState State,
    Guid? RunId,
    string? StepId,
    string Message)
{
    /// <summary>Gets the initial idle state.</summary>
    public static SimulationScenarioRunnerSnapshot Idle { get; } =
        new(SimulationScenarioRunnerState.Idle, null, null, "No scenario is running.");
}

/// <summary>Provides scenario-runner state-change event data.</summary>
/// <param name="snapshot">New runner state.</param>
public sealed class SimulationScenarioRunnerChangedEventArgs(SimulationScenarioRunnerSnapshot snapshot) : EventArgs
{
    /// <summary>Gets the new runner state.</summary>
    public SimulationScenarioRunnerSnapshot Snapshot { get; } = snapshot;
}
