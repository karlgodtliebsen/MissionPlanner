namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Associates a curated group with its supported vehicle parameters.</summary>
/// <param name="Definition">The group definition.</param>
/// <param name="Fields">The supported, non-expert fields.</param>
public sealed record ResolvedBasicTuningGroup(
    BasicTuningGroupDefinition Definition,
    IReadOnlyList<ResolvedBasicTuningField> Fields);
