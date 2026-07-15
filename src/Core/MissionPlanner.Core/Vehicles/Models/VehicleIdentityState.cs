namespace MissionPlanner.Core.Vehicles.Models;

public sealed record VehicleIdentityState(byte VehicleType, byte Autopilot, byte MavLinkVersion);
