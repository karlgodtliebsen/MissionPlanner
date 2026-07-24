namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Generates one lazy advanced tuning group across axes and instances.</summary>
/// <param name="Key">The stable descriptor key.</param>
/// <param name="Category">The search and navigation category.</param>
/// <param name="Title">The descriptor title.</param>
/// <param name="Description">The descriptor explanation.</param>
/// <param name="ParameterPrefixPattern">The prefix containing optional {axis} and {instance} tokens.</param>
/// <param name="Axes">Axis tokens, or one empty token for a non-axis descriptor.</param>
/// <param name="Instances">Instance numbers, or zero for a non-instance descriptor.</param>
/// <param name="Components">The generated parameter suffixes.</param>
/// <param name="Rules">Per-axis/per-instance coupled validation rules.</param>
/// <param name="ExpertWarning">The required expert warning.</param>
public sealed record AdvancedTuningDescriptor(
    string Key,
    string Category,
    string Title,
    string Description,
    string ParameterPrefixPattern,
    IReadOnlyList<string> Axes,
    IReadOnlyList<int> Instances,
    IReadOnlyList<AdvancedTuningComponent> Components,
    IReadOnlyList<BasicTuningRule> Rules,
    string ExpertWarning)
{
    /// <summary>Gets whether the descriptor supports copying between axes.</summary>
    public bool SupportsAxisCopy => Axes.Count(axis => !string.IsNullOrWhiteSpace(axis)) > 1;
}
