namespace MissionPlanner.Core.Setup;

/// <summary>Describes an optional-hardware configuration issue.</summary>
/// <param name="Severity">The relative severity of the issue.</param>
/// <param name="Message">The user-facing explanation.</param>
public sealed record PeripheralValidationIssue(PeripheralIssueSeverity Severity, string Message);
