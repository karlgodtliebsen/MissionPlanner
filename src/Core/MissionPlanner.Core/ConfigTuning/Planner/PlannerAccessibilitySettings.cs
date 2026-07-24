namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Configures accessibility preferences used by telemetry views.</summary>
public sealed record PlannerAccessibilitySettings
{
    /// <summary>Gets whether high-contrast telemetry presentation is requested.</summary>
    public bool HighContrastTelemetry { get; init; }

    /// <summary>Gets whether nonessential telemetry animation is reduced.</summary>
    public bool ReduceMotion { get; init; }

    /// <summary>Gets the UI text scale multiplier.</summary>
    public double TextScale { get; init; } = 1;

    /// <summary>Gets whether important telemetry warnings should be announced.</summary>
    public bool AnnounceTelemetryWarnings { get; init; } = true;
}
