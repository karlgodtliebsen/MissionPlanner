using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Describes a discovered RC configuration or calibration issue.</summary>
/// <param name="Severity">The relative severity of the issue.</param>
/// <param name="Message">The user-facing explanation.</param>
public sealed record RadioValidationIssue(RadioIssueSeverity Severity, string Message);
