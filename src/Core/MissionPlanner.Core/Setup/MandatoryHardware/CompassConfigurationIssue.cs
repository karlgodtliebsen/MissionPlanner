namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Describes a discovered configuration inconsistency for compass setup.</summary>
/// <param name="Severity">The relative severity of the issue.</param>
/// <param name="Message">The user-facing explanation.</param>
public sealed record CompassConfigurationIssue(CompassIssueSeverity Severity, string Message);
