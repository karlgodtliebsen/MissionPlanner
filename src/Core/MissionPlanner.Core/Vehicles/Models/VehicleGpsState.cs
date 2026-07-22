namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for VehicleGpsState.
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
/// <param name="SecondaryReceiver">Telemetry from GPS receiver 2, when present.</param>
public sealed record VehicleGpsState(
    GpsFixType FixType,
    int? SatellitesVisible,
    double? HorizontalDilution,
    double? VerticalDilution,
    double? GroundSpeedMetersPerSecond,
    double? CourseDegrees,
    double? HorizontalAccuracyMeters,
    double? VerticalAccuracyMeters,
    DateTimeOffset? ObservedAt,
    VehicleGpsReceiverState? SecondaryReceiver = null)
{
    /// <summary>
    /// Provides the public API for Empty.
    /// </summary>
    public static VehicleGpsState Empty { get; } = new(GpsFixType.Unknown, null, null, null, null, null, null, null, null);

    /// <summary>Returns whether primary GPS data is older than <paramref name="maximumAge"/>.</summary>
    public bool IsStale(DateTimeOffset now, TimeSpan maximumAge) => ObservedAt is null || now - ObservedAt > maximumAge;
}
