namespace MissionPlanner.Core.Setup;

/// <summary>Records one audit entry for an actuator-test operation.</summary>
/// <param name="Timestamp">The time the entry was recorded.</param>
/// <param name="Description">The operation description.</param>
/// <param name="Outcome">The operation outcome.</param>
public sealed record ActuatorTestLogEntry(DateTimeOffset Timestamp, string Description, string Outcome);
