using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

public sealed record VehicleGlobalPositionObservation(
    double LatitudeDegrees,
    double LongitudeDegrees,
    double AltitudeMslMeters,
    double? RelativeAltitudeMeters,
    double? VelocityNorthMetersPerSecond,
    double? VelocityEastMetersPerSecond,
    double? VelocityDownMetersPerSecond,
    double? HeadingDegrees,
    DateTimeOffset ObservedAt) : IVehicleObservation;
