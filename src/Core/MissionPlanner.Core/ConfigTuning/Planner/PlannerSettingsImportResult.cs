namespace MissionPlanner.Core.ConfigTuning.Planner;

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
