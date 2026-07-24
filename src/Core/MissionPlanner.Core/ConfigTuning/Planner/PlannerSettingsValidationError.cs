namespace MissionPlanner.Core.ConfigTuning.Planner;

/// <summary>Describes one invalid Planner setting.</summary>
/// <param name="Section">The settings section.</param>
/// <param name="Property">The invalid property.</param>
/// <param name="Message">A user-facing validation message.</param>
public sealed record PlannerSettingsValidationError(
    PlannerSettingsSection Section,
    string Property,
    string Message);
