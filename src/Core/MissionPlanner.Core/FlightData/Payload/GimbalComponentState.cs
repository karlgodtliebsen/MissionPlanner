namespace MissionPlanner.Core.FlightData.Payload;

/// <summary>Describes discovered gimbal state outside the autopilot aggregate.</summary>
public sealed record GimbalComponentState(PayloadComponentSelection Component, GimbalCapabilities Capabilities);
