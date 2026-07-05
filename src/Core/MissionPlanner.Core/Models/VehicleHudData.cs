namespace MissionPlanner.Core.Models;

/// <summary>
/// Represents HUD-specific data extracted from vehicle telemetry.
/// All angles are in degrees, speeds in m/s, altitude in meters.
/// </summary>
/// <param name="VehicleId">The unique identifier of the vehicle.</param>
/// <param name="Pitch">Pitch angle in degrees. Positive = nose up.</param>
/// <param name="Roll">Roll angle in degrees. Positive = right wing down.</param>
/// <param name="Heading">Heading in degrees, 0-360. 0 = North, 90 = East.</param>
/// <param name="Yaw">Yaw angle in degrees, -180 to 180.</param>
/// <param name="AirSpeed">Indicated airspeed in m/s.</param>
/// <param name="GroundSpeed">Ground speed in m/s.</param>
/// <param name="Altitude">Altitude in meters (relative to home or MSL depending on context).</param>
/// <param name="VerticalSpeed">Vertical climb/descent rate in m/s. Positive = climbing.</param>
/// <param name="BatteryVoltage">Battery voltage in volts.</param>
/// <param name="BatteryRemaining">Battery remaining percentage, 0-100.</param>
/// <param name="GpsSatellites">Number of GPS satellites in view.</param>
/// <param name="IsArmed">Whether the vehicle is armed.</param>
/// <param name="Mode">Current flight mode.</param>
/// <param name="Latitude">Current latitude position.</param>
/// <param name="Longitude">Current longitude position.</param>
public sealed record VehicleHudData(
    VehicleId VehicleId,
    double Pitch,
    double Roll,
    double Heading,
    double Yaw,
    double AirSpeed,
    double GroundSpeed,
    double Altitude,
    double VerticalSpeed,
    double BatteryVoltage,
    double BatteryRemaining,
    int GpsSatellites,
    bool IsArmed,
    VehicleMode Mode,
    double? Latitude,
    double? Longitude)
{
    /// <summary>
    /// Creates a default/empty HUD data instance for a vehicle.
    /// </summary>
    /// <param name="vehicleId">The vehicle identifier.</param>
    /// <returns>A VehicleHudData instance with default values.</returns>
    public static VehicleHudData CreateDefault(VehicleId vehicleId) => new(
        vehicleId,
        Pitch: 0,
        Roll: 0,
        Heading: 0,
        Yaw: 0,
        AirSpeed: 0,
        GroundSpeed: 0,
        Altitude: 0,
        VerticalSpeed: 0,
        BatteryVoltage: 0,
        BatteryRemaining: 0,
        GpsSatellites: 0,
        IsArmed: false,
        Mode: VehicleMode.Unknown,
        Latitude: null,
        Longitude: null);
}
