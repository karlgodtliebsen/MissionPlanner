namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Defines one curated group in a basic-tuning profile.</summary>
/// <param name="Key">The stable group key.</param>
/// <param name="Title">The group title.</param>
/// <param name="Description">The group explanation.</param>
/// <param name="Fields">The fields presented in order.</param>
/// <param name="Rules">Coupled validation rules for the group.</param>
/// <param name="Warning">An optional group-level control-stability warning.</param>
public sealed record BasicTuningGroupDefinition(
    string Key,
    string Title,
    string Description,
    IReadOnlyList<BasicTuningFieldDefinition> Fields,
    IReadOnlyList<BasicTuningRule> Rules,
    string? Warning = null);
