namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Contains connection-scoped MAVLink and firmware identity for a vehicle.
/// </summary>
/// <param name="VehicleType">The detailed MAV_TYPE value reported by HEARTBEAT.</param>
/// <param name="Autopilot">The MAV_AUTOPILOT value reported by HEARTBEAT.</param>
/// <param name="MavLinkVersion">The MAVLink version reported by HEARTBEAT.</param>
/// <param name="Firmware">The firmware and hardware identity enriched by AUTOPILOT_VERSION.</param>
public sealed record VehicleIdentityState(
    byte VehicleType,
    byte Autopilot,
    byte MavLinkVersion,
    VehicleFirmwareIdentity Firmware);
