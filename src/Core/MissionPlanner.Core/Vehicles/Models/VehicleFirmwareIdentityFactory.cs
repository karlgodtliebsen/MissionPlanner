namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Creates firmware identities from protocol-neutral vehicle observations.
/// </summary>
public static class VehicleFirmwareIdentityFactory
{
    private const byte ArduPilotMega = 3;

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
    public static FirmwareFamily MapFamily(byte mavType, byte autopilot)
    {
        if (autopilot != ArduPilotMega)
        {
            return FirmwareFamily.Unknown;
        }

        return mavType switch
        {
            1 => FirmwareFamily.ArduPlane,
            2 or 3 or 4 or 13 or 14 or 15 or 29 or 35 => FirmwareFamily.ArduCopter,
            10 or 11 => FirmwareFamily.Rover,
            12 => FirmwareFamily.ArduSub,
            5 => FirmwareFamily.AntennaTracker,
            42 => FirmwareFamily.Blimp,
            _ => FirmwareFamily.Unknown
        };
    }
}
