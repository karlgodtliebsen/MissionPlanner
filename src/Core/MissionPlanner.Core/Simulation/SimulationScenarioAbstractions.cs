namespace MissionPlanner.Core.Simulation;

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

/// <summary>Executes one declarative scenario against an exact simulator target.</summary>
public interface ISimulationScenarioRunner
{
    /// <summary>Gets current observable runner state.</summary>
    SimulationScenarioRunnerSnapshot Current { get; }

    /// <summary>Occurs when runner state changes.</summary>
    event EventHandler<SimulationScenarioRunnerChangedEventArgs>? Changed;

    /// <summary>Validates schema, exact target, modes, and controls without changing a vehicle.</summary>
    /// <param name="document">Scenario document.</param>
    /// <param name="sessionId">Exact simulation session.</param>
    /// <param name="vehicleId">Exact vehicle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dry-run validation evidence.</returns>
    Task<SimulationScenarioValidationReport> ValidateAsync(
        SimulationScenarioDocument document,
        Guid sessionId,
        MissionPlanner.Core.Vehicles.Models.VehicleId vehicleId,
        CancellationToken cancellationToken = default);

    /// <summary>Runs or dry-runs one scenario.</summary>
    /// <param name="request">Exact-target run request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete auditable report.</returns>
    Task<SimulationScenarioRunReport> RunAsync(
        SimulationScenarioRunRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Requests a pause at the next safe step boundary.</summary>
    /// <returns><see langword="true"/> when a running scenario accepted the request.</returns>
    bool Pause();

    /// <summary>Resumes a scenario paused between steps.</summary>
    /// <returns><see langword="true"/> when a paused scenario was resumed.</returns>
    bool Resume();
}

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

/// <summary>Provides cancellable delays for scenario wait polling.</summary>
public interface ISimulationScenarioDelay
{
    /// <summary>Waits for a bounded interval.</summary>
    /// <param name="delay">Delay duration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

/// <summary>Configures declarative scenario execution bounds.</summary>
public sealed class SimulationScenarioOptions
{
    /// <summary>Application configuration section.</summary>
    public const string SectionName = "SimulationScenarios";

    /// <summary>Gets or sets telemetry polling interval in milliseconds.</summary>
    public int PollIntervalMilliseconds { get; set; } = 100;

    /// <summary>Gets or sets maximum accepted scenario JSON size.</summary>
    public int MaximumDocumentBytes { get; set; } = 1_048_576;

    /// <summary>Gets or sets maximum number of steps per scenario.</summary>
    public int MaximumSteps { get; set; } = 1000;
}
