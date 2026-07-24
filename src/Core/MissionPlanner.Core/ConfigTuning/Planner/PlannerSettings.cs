namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Contains all local MissionPlanner application preferences.</summary>
public sealed record PlannerSettings
{
    /// <summary>The current persisted settings schema.</summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary>Gets the persisted schema version.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Gets display-unit settings.</summary>
    public PlannerUnitSettings Units { get; init; } = new();

    /// <summary>Gets map settings.</summary>
    public PlannerMapSettings Map { get; init; } = new();

    /// <summary>Gets telemetry presentation settings.</summary>
    public PlannerTelemetrySettings Telemetry { get; init; } = new();

    /// <summary>Gets appearance settings.</summary>
    public PlannerAppearanceSettings Appearance { get; init; } = new();

    /// <summary>Gets logging settings.</summary>
    public PlannerLoggingSettings Logging { get; init; } = new();

    /// <summary>Gets connection defaults.</summary>
    public PlannerConnectionSettings Connection { get; init; } = new();

    /// <summary>Gets parameter-cache settings.</summary>
    public PlannerParameterCacheSettings ParameterCache { get; init; } = new();

    /// <summary>Gets confirmation settings.</summary>
    public PlannerConfirmationSettings Confirmations { get; init; } = new();

    /// <summary>Gets update settings.</summary>
    public PlannerUpdateSettings Updates { get; init; } = new();

    /// <summary>Gets accessibility settings.</summary>
    public PlannerAccessibilitySettings Accessibility { get; init; } = new();
}
