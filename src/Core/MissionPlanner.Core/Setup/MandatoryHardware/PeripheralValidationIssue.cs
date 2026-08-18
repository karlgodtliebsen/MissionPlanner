namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Describes an optional-hardware configuration issue.</summary>
/// <param name="Severity">The relative severity of the issue.</param>
/// <param name="Message">The user-facing explanation.</param>
public sealed record PeripheralValidationIssue(PeripheralIssueSeverity Severity, string Message);
