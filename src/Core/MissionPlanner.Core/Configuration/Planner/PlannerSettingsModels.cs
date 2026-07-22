using System.Text.Json.Serialization;

namespace MissionPlanner.Core.Configuration.Planner;

/// <summary>Identifies the preferred display unit system.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<UnitSystem>))]
public enum UnitSystem
{
    /// <summary>Metric units.</summary>
    Metric,

    /// <summary>Imperial units.</summary>
    Imperial,

    /// <summary>Aviation and nautical units.</summary>
    Aviation
}

/// <summary>Identifies a supported base-map provider.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlannerMapProvider>))]
public enum PlannerMapProvider
{
    /// <summary>OpenStreetMap raster tiles.</summary>
    OpenStreetMap,

    /// <summary>Esri-hosted map tiles.</summary>
    Esri
}

/// <summary>Identifies the requested map presentation style.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlannerMapStyle>))]
public enum PlannerMapStyle
{
    /// <summary>Standard street-map styling.</summary>
    Standard,

    /// <summary>Satellite imagery styling.</summary>
    Topographic,

    /// <summary>Terrain-focused styling.</summary>
    Physical,

    /// <summary>Shaded-relief styling.</summary>
    ShadedRelief,

    /// <summary>Dark-gray styling.</summary>
    DarkGray
}

/// <summary>Identifies the application color theme.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlannerTheme>))]
public enum PlannerTheme
{
    /// <summary>Follow the operating-system theme.</summary>
    System,

    /// <summary>Use the light theme.</summary>
    Light,

    /// <summary>Use the dark theme.</summary>
    Dark
}

/// <summary>Identifies the configured application logging threshold.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlannerLogLevel>))]
public enum PlannerLogLevel
{
    /// <summary>Diagnostic trace logging.</summary>
    Verbose,

    /// <summary>Debug logging.</summary>
    Debug,

    /// <summary>Normal informational logging.</summary>
    Information,

    /// <summary>Warnings and errors only.</summary>
    Warning,

    /// <summary>Errors only.</summary>
    Error
}

/// <summary>Identifies how cached parameter data is reused.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ParameterCachePolicy>))]
public enum ParameterCachePolicy
{
    /// <summary>Prefer a recent cache and refresh stale data.</summary>
    PreferRecentCache,

    /// <summary>Refresh parameters whenever a connection is established.</summary>
    RefreshOnConnect,

    /// <summary>Always request a complete parameter list.</summary>
    AlwaysRefresh
}

/// <summary>Identifies a Planner settings section.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlannerSettingsSection>))]
public enum PlannerSettingsSection
{
    /// <summary>Display units.</summary>
    Units,

    /// <summary>Map defaults.</summary>
    Map,

    /// <summary>Telemetry presentation.</summary>
    Telemetry,

    /// <summary>Application appearance.</summary>
    Appearance,

    /// <summary>Logging behavior.</summary>
    Logging,

    /// <summary>Connection defaults.</summary>
    Connection,

    /// <summary>Parameter-cache behavior.</summary>
    ParameterCache,

    /// <summary>Safety confirmations.</summary>
    Confirmations,

    /// <summary>Application updates.</summary>
    Updates,

    /// <summary>Accessibility behavior.</summary>
    Accessibility
}

/// <summary>Configures display units.</summary>
public sealed record PlannerUnitSettings
{
    /// <summary>Gets the preferred unit system.</summary>
    public UnitSystem System { get; init; } = UnitSystem.Metric;
}

/// <summary>Configures map defaults.</summary>
public sealed record PlannerMapSettings
{
    /// <summary>Gets the preferred map provider.</summary>
    public PlannerMapProvider Provider { get; init; } = PlannerMapProvider.OpenStreetMap;

    /// <summary>Gets the preferred map style.</summary>
    public PlannerMapStyle Style { get; init; } = PlannerMapStyle.Standard;

    /// <summary>Gets the initial map zoom level.</summary>
    public double DefaultZoom { get; init; } = 16;
}

/// <summary>Configures telemetry presentation rates.</summary>
public sealed record PlannerTelemetrySettings
{
    /// <summary>Gets the maximum UI telemetry refresh rate in hertz.</summary>
    public int DisplayRateHz { get; init; } = 10;

    /// <summary>Gets the chart history window in seconds.</summary>
    public int ChartHistorySeconds { get; init; } = 120;
}

/// <summary>Configures application appearance.</summary>
public sealed record PlannerAppearanceSettings
{
    /// <summary>Gets the application theme.</summary>
    public PlannerTheme Theme { get; init; } = PlannerTheme.System;
}

/// <summary>Configures application logging.</summary>
public sealed record PlannerLoggingSettings
{
    /// <summary>Gets the configured logging threshold.</summary>
    public PlannerLogLevel Level { get; init; } = PlannerLogLevel.Information;

    /// <summary>Gets the log retention period in days.</summary>
    public int RetentionDays { get; init; } = 7;
}

/// <summary>Configures connection defaults without storing credentials.</summary>
public sealed record PlannerConnectionSettings
{
    /// <summary>Gets the default connection channel.</summary>
    public string Channel { get; init; } = "AUTO";

    /// <summary>Gets the default network host.</summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>Gets the default network port.</summary>
    public int Port { get; init; } = 14550;

    /// <summary>Gets the default serial baud rate.</summary>
    public int BaudRate { get; init; } = 115200;
}

/// <summary>Configures parameter-cache behavior.</summary>
public sealed record PlannerParameterCacheSettings
{
    /// <summary>Gets the cache policy.</summary>
    public ParameterCachePolicy Policy { get; init; } = ParameterCachePolicy.PreferRecentCache;

    /// <summary>Gets the maximum accepted cache age in minutes.</summary>
    public int MaximumAgeMinutes { get; init; } = 30;
}

/// <summary>Configures operation confirmation prompts.</summary>
public sealed record PlannerConfirmationSettings
{
    /// <summary>Gets whether vehicle parameter writes require confirmation.</summary>
    public bool ConfirmParameterWrites { get; init; } = true;

    /// <summary>Gets whether arm and disarm operations require confirmation.</summary>
    public bool ConfirmArmDisarm { get; init; } = true;

    /// <summary>Gets whether firmware changes require confirmation.</summary>
    public bool ConfirmFirmwareChanges { get; init; } = true;
}

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

/// <summary>Describes one invalid Planner setting.</summary>
/// <param name="Section">The settings section.</param>
/// <param name="Property">The invalid property.</param>
/// <param name="Message">A user-facing validation message.</param>
public sealed record PlannerSettingsValidationError(
    PlannerSettingsSection Section,
    string Property,
    string Message);

/// <summary>Describes the result of loading local settings.</summary>
/// <param name="Settings">The loaded or recovered settings.</param>
/// <param name="WasMigrated">Whether an older schema was migrated.</param>
/// <param name="WasRecovered">Whether corrupt or invalid data was replaced with defaults.</param>
/// <param name="Message">An optional recovery or migration message.</param>
public sealed record PlannerSettingsLoadResult(
    PlannerSettings Settings,
    bool WasMigrated,
    bool WasRecovered,
    string? Message);

/// <summary>Describes the result of saving local settings.</summary>
/// <param name="Success">Whether the settings were persisted.</param>
/// <param name="Errors">Validation errors that blocked persistence.</param>
/// <param name="RestartRequiredSections">Changed sections that take effect after restart.</param>
public sealed record PlannerSettingsSaveResult(
    bool Success,
    IReadOnlyList<PlannerSettingsValidationError> Errors,
    IReadOnlyList<PlannerSettingsSection> RestartRequiredSections);

/// <summary>Describes the result of importing local settings.</summary>
/// <param name="Success">Whether the document was accepted and persisted.</param>
/// <param name="WasMigrated">Whether the imported schema was migrated.</param>
/// <param name="Errors">Validation or format errors.</param>
/// <param name="RestartRequiredSections">Changed sections that take effect after restart.</param>
public sealed record PlannerSettingsImportResult(
    bool Success,
    bool WasMigrated,
    IReadOnlyList<PlannerSettingsValidationError> Errors,
    IReadOnlyList<PlannerSettingsSection> RestartRequiredSections);

/// <summary>Provides data for observable Planner settings changes.</summary>
public sealed class PlannerSettingsChangedEventArgs : EventArgs
{
    /// <summary>Initializes settings-change event data.</summary>
    /// <param name="previous">The previous settings.</param>
    /// <param name="current">The current settings.</param>
    /// <param name="restartRequiredSections">Changed sections that take effect after restart.</param>
    public PlannerSettingsChangedEventArgs(
        PlannerSettings previous,
        PlannerSettings current,
        IReadOnlyList<PlannerSettingsSection> restartRequiredSections)
    {
        Previous = previous;
        Current = current;
        RestartRequiredSections = restartRequiredSections;
    }

    /// <summary>Gets the previous settings.</summary>
    public PlannerSettings Previous { get; }

    /// <summary>Gets the current settings.</summary>
    public PlannerSettings Current { get; }

    /// <summary>Gets changed sections that take effect after restart.</summary>
    public IReadOnlyList<PlannerSettingsSection> RestartRequiredSections { get; }
}
