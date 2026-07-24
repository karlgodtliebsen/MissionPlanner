namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Configures update checks.</summary>
public sealed record PlannerUpdateSettings
{
    /// <summary>Gets whether update checks run automatically.</summary>
    public bool CheckAutomatically { get; init; } = true;

    /// <summary>Gets the number of days between update checks.</summary>
    public int CheckIntervalDays { get; init; } = 7;

    /// <summary>Gets the preferred update channel.</summary>
    public string Channel { get; init; } = "Stable";
}
