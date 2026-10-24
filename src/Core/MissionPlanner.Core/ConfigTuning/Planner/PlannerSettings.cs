namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>
/// Contains all local MissionPlanner application preferences.
/// </summary>
public sealed record PlannerSettings
{
    /// <summary>Gets reviewed local RTK base coordinates, using WGS84 ellipsoid height.</summary>
    public IReadOnlyList<MissionPlanner.Core.Setup.OptionalHardware.RtkBasePosition> RtkBasePositions { get; init; } = [];
    /// <summary>Gets persistent DroneCAN hardware-ID reservations for explicitly named isolated networks.</summary>
    public IReadOnlyList<MissionPlanner.Core.Setup.OptionalHardware.DroneCan.DroneCanAllocation> DroneCanAllocations { get; init; } = [];
    /// <summary>Gets local standalone tracker calibration; it never restores an active port or motion.</summary>
    public MissionPlanner.Core.Setup.OptionalHardware.SerialTrackerProfile SerialTracker { get; init; } = new();
    /// <summary>Gets local joystick calibration; vehicle output is never enabled by restoring preferences.</summary>
    public MissionPlanner.Core.Setup.OptionalHardware.JoystickProfile Joystick { get; init; } = new();
    /// <summary>Gets named live parameter subsets. These store names only, never vehicle parameter values.</summary>
    public IReadOnlyDictionary<string, string[]> ParameterCollections { get; init; } = new Dictionary<string, string[]>();

    /// <summary>Gets parameter names pinned first in the ADS-B editor.</summary>
    public IReadOnlyList<string> AdsbFavorites { get; init; } = [];

    /// <summary>The current persisted settings schema.</summary>
    public const int CurrentSchemaVersion = 5;

    /// <summary>Gets the persisted schema version.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Gets display-unit settings.</summary>
    public PlannerUnitSettings Units { get; init; } = new();

    /// <summary>Gets map settings.</summary>
    public PlannerMapSettings Map { get; init; } = new();

    /// <summary>Gets telemetry presentation settings.</summary>
    public PlannerTelemetrySettings Telemetry { get; init; } = new();

    /// <summary>Gets appearance settings.</summary>
    public PlannerAppearanceSettings Appearance { get; set; } = new();

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

    /// <summary>Gets established MissionPlanner operational preferences.</summary>
    public PlannerLegacySettings Legacy { get; init; } = new();
}
