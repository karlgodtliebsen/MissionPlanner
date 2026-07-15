namespace MissionPlanner.Core.Vehicles.Models;

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
    public static VehicleHealthState Empty { get; } = new(null, null, null, null, null, null, null, null, null);
}
