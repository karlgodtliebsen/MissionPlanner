namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Projects the aggregate result of applying a group of parameter edits.</summary>
/// <param name="Success">Whether every modified field was confirmed.</param>
/// <param name="Results">The per-field results.</param>
/// <param name="RebootRequired">Whether any confirmed change requires a reboot.</param>
public sealed record ParameterApplyReport(bool Success, IReadOnlyList<ParameterWriteResult> Results, bool RebootRequired)
{
    /// <summary>Gets the fields that were confirmed.</summary>
    public IReadOnlyList<string> Confirmed => Results.Where(result => result.Outcome == ParameterWriteOutcome.Confirmed).Select(result => result.Name).ToArray();

    /// <summary>Gets the fields that still need attention.</summary>
    public IReadOnlyList<string> Failed => Results
        .Where(result => result.Outcome is ParameterWriteOutcome.WriteFailed or ParameterWriteOutcome.ReadbackFailed or ParameterWriteOutcome.ValidationFailed)
        .Select(result => result.Name)
        .ToArray();
}
