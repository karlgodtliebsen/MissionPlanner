namespace MissionPlanner.Core.ConfigTuning.Planner;

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
