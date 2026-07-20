using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>
/// Provides the public API for VehicleGpsObservation.
/// </summary>
/// <param name="FixType">The FixType value.</param>
/// <param name="SatellitesVisible">The SatellitesVisible value.</param>
/// <param name="HorizontalDilution">The HorizontalDilution value.</param>
/// <param name="VerticalDilution">The VerticalDilution value.</param>
/// <param name="GroundSpeedMetersPerSecond">The GroundSpeedMetersPerSecond value.</param>
/// <param name="CourseDegrees">The CourseDegrees value.</param>
/// <param name="HorizontalAccuracyMeters">The HorizontalAccuracyMeters value.</param>
/// <param name="VerticalAccuracyMeters">The VerticalAccuracyMeters value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
public sealed record VehicleGpsObservation(
    GpsFixType FixType,
    int? SatellitesVisible,
    double? HorizontalDilution,
    double? VerticalDilution,
    double? GroundSpeedMetersPerSecond,
    double? CourseDegrees,
    double? HorizontalAccuracyMeters,
    double? VerticalAccuracyMeters,
    DateTimeOffset ObservedAt) : IVehicleObservation;
