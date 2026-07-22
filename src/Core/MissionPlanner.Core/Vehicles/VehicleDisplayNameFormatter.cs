using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Formats the derived user-facing name of a MAVLink vehicle.
/// </summary>
public static class VehicleDisplayNameFormatter
{
    /// <summary>
    /// Formats a vehicle name from its structured MAVLink identity.
    /// </summary>
    /// <param name="vehicleId">The structured system and component identifier.</param>
    /// <param name="family">The derived firmware family.</param>
    /// <param name="mavType">The detailed MAV_TYPE value.</param>
    /// <returns>A display name such as <c>SysID 1:Copter</c>.</returns>
    public static string Format(VehicleId vehicleId, FirmwareFamily family, byte mavType)
    {
        var typeName = family switch
        {
            FirmwareFamily.ArduCopter => "Copter",
            FirmwareFamily.ArduPlane => "Plane",
            FirmwareFamily.Rover => "Rover",
            FirmwareFamily.ArduSub => "Sub",
            FirmwareFamily.AntennaTracker => "Tracker",
            FirmwareFamily.APPeriph => "Peripheral",
            FirmwareFamily.Blimp => "Blimp",
            _ => FormatMavType(mavType)
        };

        return $"SysID {vehicleId.SystemId}:{typeName}";
    }

    private static string FormatMavType(byte mavType) => mavType switch
    {
        1 => "Fixed Wing",
        2 => "Quadrotor",
        3 => "Coaxial",
        4 => "Helicopter",
        5 => "Antenna Tracker",
        10 => "Ground Rover",
        11 => "Surface Boat",
        12 => "Submarine",
        13 => "Hexarotor",
        14 => "Octorotor",
        15 => "Tricopter",
        29 => "Dodecarotor",
        35 => "Decarotor",
        42 => "Blimp",
        _ => "Unknown"
    };
}
