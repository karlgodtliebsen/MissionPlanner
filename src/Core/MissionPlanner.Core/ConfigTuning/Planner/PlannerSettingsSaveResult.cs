namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Describes the result of saving local settings.</summary>
/// <param name="Success">Whether the settings were persisted.</param>
/// <param name="Errors">Validation errors that blocked persistence.</param>
/// <param name="RestartRequiredSections">Changed sections that take effect after restart.</param>
public sealed record PlannerSettingsSaveResult(
    bool Success,
    IReadOnlyList<PlannerSettingsValidationError> Errors,
    IReadOnlyList<PlannerSettingsSection> RestartRequiredSections);
