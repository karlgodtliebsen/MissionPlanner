using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;

namespace MissionPlanner.Core.Vehicles.Observations;

/// <summary>
/// Provides the public API for VehicleGpsObservation.
/// </summary>
/// <param name="FixType">The FixType value.</param>
/// <param name="SatellitesVisible">The SatellitesVisible value.</param>
/// <param name="HorizontalDilution">The HorizontalDilution value.</param>
/// <param name="VerticalDilution">The VerticalDilution value.</param>
/// <param name="GroundSpeedMetersPerSecond">The GroundSpeedMetersPerSecond value.</param>
/// <param name="CourseDegrees">The CourseDegrees value.</param>
/// <param name="HorizontalAccuracyMeters">The HorizontalAccuracyMeters value.</param>
/// <param name="VerticalAccuracyMeters">The VerticalAccuracyMeters value.</param>
/// <param name="ObservedAt">The ObservedAt value.</param>
/// <param name="ReceiverIndex">The zero-based GPS receiver index.</param>
public sealed record VehicleGpsObservation(
    GpsFixType FixType,
    int? SatellitesVisible,
    double? HorizontalDilution,
    double? VerticalDilution,
    double? GroundSpeedMetersPerSecond,
    double? CourseDegrees,
    double? HorizontalAccuracyMeters,
    double? VerticalAccuracyMeters,
    DateTimeOffset ObservedAt,
    int ReceiverIndex = 0) : IVehicleObservation
{
    /// <summary>Gets the receiver latitude, separately from fused vehicle position.</summary>
    public double? LatitudeDegrees { get; init; }
    /// <summary>Gets the receiver longitude.</summary>
    public double? LongitudeDegrees { get; init; }
    /// <summary>Gets the receiver MSL altitude.</summary>
    public double? AltitudeMslMeters { get; init; }
    /// <summary>Gets ellipsoid minus MSL altitude when the extension is available.</summary>
    public double? GeoidSeparationMeters { get; init; }
}
