namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Associates one descriptor with the fields present on a vehicle.</summary>
/// <param name="Descriptor">The descriptor.</param>
/// <param name="Fields">The supported expanded fields.</param>
public sealed record ResolvedAdvancedTuningGroup(
    AdvancedTuningDescriptor Descriptor,
    IReadOnlyList<ResolvedAdvancedTuningField> Fields)
{
    /// <summary>Gets the present axis names in stable order.</summary>
    public IReadOnlyList<string> Axes => Fields
        .Select(item => item.Definition.Axis)
        .Where(axis => !string.IsNullOrWhiteSpace(axis))
        .Distinct(StringComparer.Ordinal)
        .ToArray();
}
