using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>
/// Provides the public API for VehicleNavigationObservation.
/// </summary>
/// <param name="DesiredRollDegrees">The DesiredRollDegrees value.</param>
/// <param name="DesiredPitchDegrees">The DesiredPitchDegrees value.</param>
/// <param name="NavigationBearingDegrees">The NavigationBearingDegrees value.</param>
/// <param name="TargetBearingDegrees">The TargetBearingDegrees value.</param>
/// <param name="WaypointDistanceMeters">The WaypointDistanceMeters value.</param>
/// <param name="AltitudeErrorMeters">The AltitudeErrorMeters value.</param>
/// <param name="AirspeedErrorMetersPerSecond">The AirspeedErrorMetersPerSecond value.</param>
/// <param name="CrossTrackErrorMeters">The CrossTrackErrorMeters value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
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
