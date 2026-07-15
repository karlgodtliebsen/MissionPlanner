namespace MissionPlanner.Core.Models;

public sealed record VehiclePositionState(
    double? LatitudeDegrees,
    double? LongitudeDegrees,
    double? AltitudeMslMeters,
    double? RelativeAltitudeMeters,
    double? LocalNorthMeters,
    double? LocalEastMeters,
    double? LocalDownMeters,
    double? HeadingDegrees,
    DateTimeOffset? ObservedAt)
{
    public static VehiclePositionState Empty { get; } = new(null, null, null, null, null, null, null, null, null);
}
