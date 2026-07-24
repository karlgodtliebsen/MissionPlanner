namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Describes a coupled validation failure.</summary>
/// <param name="GroupKey">The affected group key.</param>
/// <param name="ParameterNames">The involved resolved parameter names.</param>
/// <param name="Message">The user-facing validation message.</param>
public sealed record BasicTuningValidationIssue(
    string GroupKey,
    IReadOnlyList<string> ParameterNames,
    string Message);
