using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

public sealed record VehicleNavigationObservation(
    double DesiredRollDegrees,
    double DesiredPitchDegrees,
    double NavigationBearingDegrees,
    double TargetBearingDegrees,
    double WaypointDistanceMeters,
    double AltitudeErrorMeters,
    double AirspeedErrorMetersPerSecond,
    double CrossTrackErrorMeters,
    DateTimeOffset ObservedAt) : IVehicleObservation;
