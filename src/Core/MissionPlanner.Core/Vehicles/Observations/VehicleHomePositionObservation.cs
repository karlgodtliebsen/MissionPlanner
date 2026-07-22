using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>
/// Observation of the vehicle's home (launch) position from a HOME_POSITION message.
/// </summary>
/// <param name="LatitudeDegrees">The home latitude in degrees.</param>
/// <param name="LongitudeDegrees">The home longitude in degrees.</param>
/// <param name="AltitudeMslMeters">The home altitude above mean sea level in meters.</param>
/// <param name="ObservedAt">The timestamp when the observation was made.</param>
public sealed record VehicleHomePositionObservation(
    double LatitudeDegrees,
    double LongitudeDegrees,
    double AltitudeMslMeters,
    DateTimeOffset ObservedAt) : IVehicleObservation;
