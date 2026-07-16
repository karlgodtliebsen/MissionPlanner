namespace MissionPlanner.Core.Vehicles.Models;

public sealed record VehiclePositionState(
    double? LatitudeDegrees,
    double? LongitudeDegrees,
    double? AltitudeMslMeters,
    double? RelativeAltitudeMeters,
    double? LocalNorthMeters,
    double? LocalEastMeters,
    double? LocalDownMeters,
    double? HeadingDegrees,
    DateTimeOffset? ObservedAt,
    double? HomeLatitudeDegrees = null,
    double? HomeLongitudeDegrees = null,
    double? HomeAltitudeMslMeters = null)
{
    public static VehiclePositionState Empty { get; } = new(null, null, null, null, null, null, null, null, null);
}
