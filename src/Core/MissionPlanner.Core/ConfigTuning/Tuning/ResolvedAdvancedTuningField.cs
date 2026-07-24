namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Associates an expanded advanced field with a present vehicle parameter.</summary>
/// <param name="Definition">The expanded field definition.</param>
/// <param name="ParameterName">The resolved parameter name.</param>
public sealed record ResolvedAdvancedTuningField(
    AdvancedTuningFieldDefinition Definition,
    string ParameterName);
