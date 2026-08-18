namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Projects one titled section of a setup summary.</summary>
/// <param name="Title">The section title.</param>
/// <param name="Entries">The section entries.</param>
public sealed record SetupSummarySection(string Title, IReadOnlyList<SetupSummaryEntry> Entries);
