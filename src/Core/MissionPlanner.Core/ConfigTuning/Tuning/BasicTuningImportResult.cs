namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Reports an import into the currently presented tuning fields.</summary>
/// <param name="Success">Whether the import was valid and applied to pending state.</param>
/// <param name="ImportedCount">The number of presented values imported.</param>
/// <param name="IgnoredNames">Names not presented by the current profile.</param>
/// <param name="Errors">Parse, metadata, family, or coupled validation errors.</param>
public sealed record BasicTuningImportResult(
    bool Success,
    int ImportedCount,
    IReadOnlyList<string> IgnoredNames,
    IReadOnlyList<string> Errors);
