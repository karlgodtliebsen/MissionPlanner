namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Contains telemetry for an additional GPS receiver.</summary>
/// <param name="FixType">The receiver fix type.</param>
/// <param name="SatellitesVisible">The visible satellite count, or <see langword="null"/> when unknown.</param>
/// <param name="HorizontalDilution">The horizontal dilution of precision.</param>
/// <param name="VerticalDilution">The vertical dilution of precision.</param>
/// <param name="GroundSpeedMetersPerSecond">The ground speed in metres per second.</param>
/// <param name="CourseDegrees">The course over ground in degrees.</param>
/// <param name="HorizontalAccuracyMeters">The horizontal accuracy in metres.</param>
/// <param name="VerticalAccuracyMeters">The vertical accuracy in metres.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleGpsReceiverState(
    GpsFixType FixType,
    int? SatellitesVisible,
    double? HorizontalDilution,
    double? VerticalDilution,
    double? GroundSpeedMetersPerSecond,
    double? CourseDegrees,
    double? HorizontalAccuracyMeters,
    double? VerticalAccuracyMeters,
    DateTimeOffset ObservedAt)
{
    /// <summary>Returns whether this receiver state is older than <paramref name="maximumAge"/>.</summary>
    public bool IsStale(DateTimeOffset now, TimeSpan maximumAge) => now - ObservedAt > maximumAge;
}
