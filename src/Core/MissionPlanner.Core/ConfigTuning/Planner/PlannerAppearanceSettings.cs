namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Configures application appearance.</summary>
public sealed record PlannerAppearanceSettings
{
    /// <summary>Gets the application theme.</summary>
    public PlannerTheme Theme { get; init; } = PlannerTheme.System;
}
