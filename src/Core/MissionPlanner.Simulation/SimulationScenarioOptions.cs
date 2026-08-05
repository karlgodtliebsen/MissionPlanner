namespace MissionPlanner.Core.Simulation;

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
