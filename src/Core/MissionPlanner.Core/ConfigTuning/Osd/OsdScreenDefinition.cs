namespace MissionPlanner.Core.ConfigTuning.Osd;

/// <summary>Defines one discovered OSD screen and its character-grid capabilities.</summary>
/// <param name="Number">The one-based screen number.</param>
/// <param name="Title">The screen title.</param>
/// <param name="GridWidth">The discovered character-grid width.</param>
/// <param name="GridHeight">The discovered character-grid height.</param>
/// <param name="SupportsDynamicOverlaps">Whether metadata advertises dynamic overlapping items.</param>
/// <param name="ScreenParameterNames">Screen enable/options/font/resolution parameters.</param>
/// <param name="Items">The discovered screen items.</param>
public sealed record OsdScreenDefinition(
    int Number,
    string Title,
    int GridWidth,
    int GridHeight,
    bool SupportsDynamicOverlaps,
    IReadOnlyList<string> ScreenParameterNames,
    IReadOnlyList<OsdItemDefinition> Items);
