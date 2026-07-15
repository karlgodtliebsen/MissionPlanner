namespace MissionPlanner.Core.Models.Observations;

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
