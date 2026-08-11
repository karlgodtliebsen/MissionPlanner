namespace MissionPlanner.Core.FlightData.Payload;

/// <summary>Describes conservative gimbal capabilities.</summary>
public sealed record GimbalCapabilities(bool PitchYaw, bool YawLock, bool ManualControl);
