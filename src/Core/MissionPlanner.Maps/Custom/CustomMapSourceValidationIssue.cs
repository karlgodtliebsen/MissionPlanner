namespace MissionPlanner.Maps.Custom;

/// <summary>Represents a custom source validation message.</summary>
/// <param name="Path">Configuration path.</param>
/// <param name="Message">Message text.</param>
/// <param name="IsWarning">Whether the message is advisory rather than invalidating.</param>
public sealed record CustomMapSourceValidationIssue(string Path, string Message, bool IsWarning);
