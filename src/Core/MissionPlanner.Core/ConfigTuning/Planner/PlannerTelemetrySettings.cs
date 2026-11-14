namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Configures telemetry presentation and refresh rates.</summary>
public sealed record PlannerTelemetrySettings
{
    /// <summary>Gets the default attitude presentation in Flight Data.</summary>
    public AttitudeDisplayMode DefaultAttitudeDisplay { get; init; } = AttitudeDisplayMode.Both;

    /// <summary>Gets the maximum UI telemetry refresh rate in hertz.</summary>
    public int DisplayRateHz { get; init; } = 10;

    /// <summary>Gets the chart history window in seconds.</summary>
    public int ChartHistorySeconds { get; init; } = 120;
}
