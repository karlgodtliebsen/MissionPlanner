using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Describes a discovered battery configuration issue.</summary>
/// <param name="Severity">The relative severity of the issue.</param>
/// <param name="Message">The user-facing explanation.</param>
public sealed record BatteryValidationIssue(BatteryIssueSeverity Severity, string Message);
