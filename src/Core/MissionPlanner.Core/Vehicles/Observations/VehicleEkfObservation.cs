using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>
/// Provides the public API for VehicleEkfObservation.
/// </summary>
/// <param name="Flags">The Flags value.</param>
/// <param name="IsHealthy">The IsHealthy value.</param>
/// <param name="VelocityVariance">The VelocityVariance value.</param>
/// <param name="HorizontalPositionVariance">The HorizontalPositionVariance value.</param>
/// <param name="VerticalPositionVariance">The VerticalPositionVariance value.</param>
/// <param name="CompassVariance">The CompassVariance value.</param>
/// <param name="TerrainAltitudeVariance">The TerrainAltitudeVariance value.</param>
/// <param name="AirspeedVariance">The AirspeedVariance value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
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
