using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>
/// Provides the public API for VehicleLocalPositionObservation.
/// </summary>
/// <param name="NorthMeters">The NorthMeters value.</param>
/// <param name="EastMeters">The EastMeters value.</param>
/// <param name="DownMeters">The DownMeters value.</param>
/// <param name="VelocityNorthMetersPerSecond">The VelocityNorthMetersPerSecond value.</param>
/// <param name="VelocityEastMetersPerSecond">The VelocityEastMetersPerSecond value.</param>
/// <param name="VelocityDownMetersPerSecond">The VelocityDownMetersPerSecond value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
public sealed record VehicleLocalPositionObservation(
    double NorthMeters,
    double EastMeters,
    double DownMeters,
    double VelocityNorthMetersPerSecond,
    double VelocityEastMetersPerSecond,
    double VelocityDownMetersPerSecond,
    DateTimeOffset ObservedAt) : IVehicleObservation;
