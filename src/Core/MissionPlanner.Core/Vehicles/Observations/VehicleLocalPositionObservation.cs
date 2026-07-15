using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

public sealed record VehicleLocalPositionObservation(
    double NorthMeters,
    double EastMeters,
    double DownMeters,
    double VelocityNorthMetersPerSecond,
    double VelocityEastMetersPerSecond,
    double VelocityDownMetersPerSecond,
    DateTimeOffset ObservedAt) : IVehicleObservation;
