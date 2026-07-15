using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles.Observations;

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
