namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Projects one labelled entry in a setup summary section.</summary>
/// <param name="Label">The entry label.</param>
/// <param name="Value">The entry value.</param>
/// <param name="Status">The assessed status of the entry.</param>
public sealed record SetupSummaryEntry(string Label, string Value, SetupAssessmentStatus Status);
