using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>
/// Represents an AHRS-based observation of the vehicle's attitude and position.
/// This observation is typically used as a fallback when higher-priority telemetry
/// (such as ATTITUDE or GLOBAL_POSITION_INT) is unavailable.
/// </summary>
/// <param name="RollRadians">Vehicle roll in radians.</param>
/// <param name="PitchRadians">Vehicle pitch in radians.</param>
/// <param name="YawRadians">Vehicle yaw in radians.</param>
/// <param name="LatitudeDegrees">Latitude in decimal degrees.</param>
/// <param name="LongitudeDegrees">Longitude in decimal degrees.</param>
/// <param name="AltitudeMslMeters">Altitude above mean sea level in meters.</param>
/// <param name="IsEstimated"></param>
/// <param name="ObservedAt">Timestamp when the observation was received.</param>
public sealed record VehicleAhrsObservation(
    double RollRadians,
    double PitchRadians,
    double YawRadians,
    double LatitudeDegrees,
    double LongitudeDegrees,
    double AltitudeMslMeters,
    bool IsEstimated,
    DateTimeOffset ObservedAt) : IVehicleObservation;
