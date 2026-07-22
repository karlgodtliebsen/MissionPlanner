namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Creates firmware identities from protocol-neutral vehicle observations.
/// </summary>
public static class VehicleFirmwareIdentityFactory
{
    /// <summary>Creates the initial identity reported by a heartbeat.</summary>
    /// <param name="mavType">The MAV_TYPE value.</param>
    /// <param name="autopilot">The MAV_AUTOPILOT value.</param>
    /// <returns>A clean connection-scoped firmware identity.</returns>
    public static VehicleFirmwareIdentity FromHeartbeat(byte mavType, byte autopilot) => new(
        MapFamily(mavType, autopilot), mavType, autopilot, null, null, 0, 0, 0, 0, null, null);

    /// <summary>Maps MAV type and autopilot values to a broad firmware family.</summary>
    /// <param name="mavType">The MAV_TYPE value.</param>
    /// <param name="autopilot">The MAV_AUTOPILOT value.</param>
    /// <returns>The mapped family, or <see cref="FirmwareFamily.Unknown"/>.</returns>
    public static FirmwareFamily MapFamily(byte mavType, byte autopilot) =>
        ((MavLink.Generated.MavType)mavType).ToFirmwareFamily((MavLink.Generated.MavAutopilot)autopilot);

    /// <summary>Maps generated MAVLink identity enums to a broad firmware family.</summary>
    /// <param name="mavType">The generated MAV_TYPE value.</param>
    /// <param name="autopilot">The generated MAV_AUTOPILOT value.</param>
    /// <returns>The mapped family, or <see cref="FirmwareFamily.Unknown"/>.</returns>
    public static FirmwareFamily MapFamily(
        MavLink.Generated.MavType mavType,
        MavLink.Generated.MavAutopilot autopilot) =>
        mavType.ToFirmwareFamily(autopilot);
}
