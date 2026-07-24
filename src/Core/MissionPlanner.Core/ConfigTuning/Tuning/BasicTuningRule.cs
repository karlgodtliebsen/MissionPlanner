using MissionPlanner.Core.ConfigTuning;

namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Defines a coupled validation rule between two logical tuning fields.</summary>
/// <param name="Kind">The relationship to validate.</param>
/// <param name="FirstFieldKey">The first logical field key.</param>
/// <param name="SecondFieldKey">The second logical field key.</param>
/// <param name="Message">The plain-language validation message.</param>
public sealed record BasicTuningRule(
    BasicTuningRuleKind Kind,
    string FirstFieldKey,
    string SecondFieldKey,
    string Message);
