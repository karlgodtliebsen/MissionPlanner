namespace MissionPlanner.Core.ConfigTuning.Tuning;

/// <summary>Defines the expanded form of one advanced descriptor field.</summary>
/// <param name="DescriptorKey">The source descriptor key.</param>
/// <param name="Category">The source category.</param>
/// <param name="Axis">The axis token, when applicable.</param>
/// <param name="Instance">The instance number, when applicable.</param>
/// <param name="Component">The component definition.</param>
/// <param name="Parameter">The exact vehicle parameter definition.</param>
public sealed record AdvancedTuningFieldDefinition(
    string DescriptorKey,
    string Category,
    string Axis,
    int Instance,
    AdvancedTuningComponent Component,
    ParameterFieldDefinition Parameter);
