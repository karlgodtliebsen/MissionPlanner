using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

public sealed record VehicleHudObservation(
    double AirSpeedMetersPerSecond,
    double GroundSpeedMetersPerSecond,
    double HeadingDegrees,
    double AltitudeMslMeters,
    double VerticalSpeedMetersPerSecond,
    DateTimeOffset ObservedAt) : IVehicleObservation;
