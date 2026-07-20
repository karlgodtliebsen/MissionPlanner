using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>
/// Provides the public API for VehicleGlobalPositionObservation.
/// </summary>
/// <param name="LatitudeDegrees">The LatitudeDegrees value.</param>
/// <param name="LongitudeDegrees">The LongitudeDegrees value.</param>
/// <param name="AltitudeMslMeters">The AltitudeMslMeters value.</param>
/// <param name="RelativeAltitudeMeters">The RelativeAltitudeMeters value.</param>
/// <param name="VelocityNorthMetersPerSecond">The VelocityNorthMetersPerSecond value.</param>
/// <param name="VelocityEastMetersPerSecond">The VelocityEastMetersPerSecond value.</param>
/// <param name="VelocityDownMetersPerSecond">The VelocityDownMetersPerSecond value.</param>
/// <param name="HeadingDegrees">The HeadingDegrees value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
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
