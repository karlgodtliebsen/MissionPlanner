namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Configures application logging.</summary>
public sealed record PlannerLoggingSettings
{
    /// <summary>Gets the configured logging threshold.</summary>
    public PlannerLogLevel Level { get; init; } = PlannerLogLevel.Information;

    /// <summary>Gets the log retention period in days.</summary>
    public int RetentionDays { get; init; } = 7;
}
