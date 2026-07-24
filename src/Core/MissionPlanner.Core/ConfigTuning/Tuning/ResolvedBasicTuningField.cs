namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Associates a curated field with the parameter name present on a vehicle.</summary>
/// <param name="Definition">The curated field definition.</param>
/// <param name="ParameterName">The resolved live parameter name.</param>
public sealed record ResolvedBasicTuningField(
    BasicTuningFieldDefinition Definition,
    string ParameterName);
