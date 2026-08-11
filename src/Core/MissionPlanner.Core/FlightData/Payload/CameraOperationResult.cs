namespace MissionPlanner.Core.FlightData.Payload;

/// <summary>Reports a component-targeted camera operation.</summary>
public sealed record CameraOperationResult(bool Accepted, string Summary);
