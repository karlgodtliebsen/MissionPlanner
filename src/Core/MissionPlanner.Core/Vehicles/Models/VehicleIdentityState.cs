namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for VehicleIdentityState.
/// </summary>
public sealed record VehicleIdentityState(byte VehicleType, byte Autopilot, byte MavLinkVersion);
