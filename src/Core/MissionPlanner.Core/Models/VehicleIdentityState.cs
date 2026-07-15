namespace MissionPlanner.Core.Models;

public sealed record VehicleIdentityState(byte VehicleType, byte Autopilot, byte MavLinkVersion);
