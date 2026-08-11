namespace MissionPlanner.Core.FlightData.Payload;

/// <summary>Reports a component-targeted gimbal operation.</summary>
public sealed record GimbalOperationResult(bool Accepted, string Summary);
