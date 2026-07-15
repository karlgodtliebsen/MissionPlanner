namespace MissionPlanner.MavLink.Parameters.Metadata;

/// <summary>
/// Utility class for vehicle type related operations.
/// </summary>
public static class VehicleTypeUtil
{
    /// <summary>
    /// Gets the XML element name for a given vehicle type.
    /// </summary>
    /// <param name="vehicleType">The vehicle type.</param>
    /// <returns>The XML element name corresponding to the vehicle type.</returns>
    /// <exception cref="ArgumentException">Thrown when the vehicle type is unknown.</exception>
    public static string GetVehicleElementName(VehicleType vehicleType)
    {
        return vehicleType switch
        {
            VehicleType.ArduCopter => "ArduCopter",
            VehicleType.ArduPlane => "ArduPlane",
            VehicleType.Rover => "Rover",
            VehicleType.ArduSub => "ArduSub",
            VehicleType.AntennaTracker => "AntennaTracker",
            VehicleType.AP_Periph => "AP_Periph",
            VehicleType.SITL => "SITL",
            VehicleType.Blimp => "Blimp",
            VehicleType.Heli => "Heli",
            var _ => throw new ArgumentException($"Unknown vehicle type: {vehicleType}", nameof(vehicleType))
        };
    }
}
