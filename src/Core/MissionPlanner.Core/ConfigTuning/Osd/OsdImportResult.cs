namespace MissionPlanner.Core.ConfigTuning.Osd;

/// <summary>Reports a layout import into pending state.</summary>
/// <param name="Success">Whether the import was valid and applied atomically.</param>
/// <param name="ImportedCount">The number of imported discovered parameters.</param>
/// <param name="IgnoredNames">Parameters not discovered on the active firmware.</param>
/// <param name="Issues">Layout validation issues.</param>
/// <param name="Errors">Format, scope, or metadata errors.</param>
public sealed record OsdImportResult(
    bool Success,
    int ImportedCount,
    IReadOnlyList<string> IgnoredNames,
    IReadOnlyList<OsdValidationIssue> Issues,
    IReadOnlyList<string> Errors);
