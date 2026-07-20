namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for VehicleHealthState.
/// </summary>
/// <param name="EkfFlags">The EkfFlags value.</param>
/// <param name="EkfHealthy">The EkfHealthy value.</param>
/// <param name="VelocityVariance">The VelocityVariance value.</param>
/// <param name="HorizontalPositionVariance">The HorizontalPositionVariance value.</param>
/// <param name="VerticalPositionVariance">The VerticalPositionVariance value.</param>
/// <param name="CompassVariance">The CompassVariance value.</param>
/// <param name="TerrainAltitudeVariance">The TerrainAltitudeVariance value.</param>
/// <param name="AirspeedVariance">The AirspeedVariance value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
public sealed record VehicleHealthState(
    ushort? EkfFlags,
    bool? EkfHealthy,
    double? VelocityVariance,
    double? HorizontalPositionVariance,
    double? VerticalPositionVariance,
    double? CompassVariance,
    double? TerrainAltitudeVariance,
    double? AirspeedVariance,
    DateTimeOffset? ObservedAt)
{
    /// <summary>
    /// Provides the public API for Empty.
    /// </summary>
    public static VehicleHealthState Empty { get; } = new(null, null, null, null, null, null, null, null, null);
}
