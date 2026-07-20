using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>
/// Provides the public API for VehicleHudObservation.
/// </summary>
/// <param name="AirSpeedMetersPerSecond">The AirSpeedMetersPerSecond value.</param>
/// <param name="GroundSpeedMetersPerSecond">The GroundSpeedMetersPerSecond value.</param>
/// <param name="HeadingDegrees">The HeadingDegrees value.</param>
/// <param name="AltitudeMslMeters">The AltitudeMslMeters value.</param>
/// <param name="VerticalSpeedMetersPerSecond">The VerticalSpeedMetersPerSecond value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
public sealed record VehicleHudObservation(
    double AirSpeedMetersPerSecond,
    double GroundSpeedMetersPerSecond,
    double HeadingDegrees,
    double AltitudeMslMeters,
    double VerticalSpeedMetersPerSecond,
    DateTimeOffset ObservedAt) : IVehicleObservation;
