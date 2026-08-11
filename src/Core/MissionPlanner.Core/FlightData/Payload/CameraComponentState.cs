namespace MissionPlanner.Core.FlightData.Payload;

/// <summary>Describes discovered camera state outside the autopilot aggregate.</summary>
public sealed record CameraComponentState(PayloadComponentSelection Component, CameraCapabilities Capabilities);
