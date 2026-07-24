namespace MissionPlanner.Core.ConfigTuning.Osd;

/// <summary>Describes a grid-bound or overlap validation result.</summary>
/// <param name="Severity">The result severity.</param>
/// <param name="ScreenNumber">The affected screen.</param>
/// <param name="ItemKeys">The affected item keys.</param>
/// <param name="Message">The user-facing explanation.</param>
public sealed record OsdValidationIssue(
    OsdValidationSeverity Severity,
    int ScreenNumber,
    IReadOnlyList<string> ItemKeys,
    string Message);
