using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

public sealed record VehicleEkfObservation(
    ushort Flags,
    bool IsHealthy,
    double VelocityVariance,
    double HorizontalPositionVariance,
    double VerticalPositionVariance,
    double CompassVariance,
    double TerrainAltitudeVariance,
    double? AirspeedVariance,
    DateTimeOffset ObservedAt) : IVehicleObservation;
