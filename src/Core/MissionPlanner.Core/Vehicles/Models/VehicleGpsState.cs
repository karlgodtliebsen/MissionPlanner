namespace MissionPlanner.Core.Models;

public sealed record VehicleGpsState(
    GpsFixType FixType,
    int? SatellitesVisible,
    double? HorizontalDilution,
    double? VerticalDilution,
    double? GroundSpeedMetersPerSecond,
    double? CourseDegrees,
    double? HorizontalAccuracyMeters,
    double? VerticalAccuracyMeters,
    DateTimeOffset? ObservedAt)
{
    public static VehicleGpsState Empty { get; } = new(GpsFixType.Unknown, null, null, null, null, null, null, null, null);
}
