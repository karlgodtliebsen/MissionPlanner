namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Describes an advanced cross-field validation failure.</summary>
/// <param name="DescriptorKey">The affected descriptor.</param>
/// <param name="ParameterNames">The involved parameters.</param>
/// <param name="Message">The user-facing explanation.</param>
public sealed record ExtendedTuningValidationIssue(
    string DescriptorKey,
    IReadOnlyList<string> ParameterNames,
    string Message);
