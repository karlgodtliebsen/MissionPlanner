namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>Contains terrain, wind, and altitude environment telemetry.</summary>
/// <param name="WindNorthMetersPerSecond">The north wind component in metres per second.</param>
/// <param name="WindEastMetersPerSecond">The east wind component in metres per second.</param>
/// <param name="WindDownMetersPerSecond">The down wind component in metres per second.</param>
/// <param name="WindHorizontalVariance">The horizontal wind variance.</param>
/// <param name="WindVerticalVariance">The vertical wind variance.</param>
/// <param name="TerrainHeightMslMeters">The terrain height above MSL in metres.</param>
/// <param name="HeightAboveTerrainMeters">The current height above terrain in metres.</param>
/// <param name="AltitudeMonotonicMeters">The monotonic altitude in metres.</param>
/// <param name="AltitudeMslMeters">The MSL altitude in metres.</param>
/// <param name="AltitudeLocalMeters">The local altitude in metres.</param>
/// <param name="AltitudeRelativeMeters">The relative altitude in metres.</param>
/// <param name="BottomClearanceMeters">The bottom clearance in metres.</param>
/// <param name="ObservedAt">The reception timestamp.</param>
public sealed record VehicleEnvironmentState(double? WindNorthMetersPerSecond, double? WindEastMetersPerSecond, double? WindDownMetersPerSecond, double? WindHorizontalVariance, double? WindVerticalVariance, double? TerrainHeightMslMeters, double? HeightAboveTerrainMeters, double? AltitudeMonotonicMeters, double? AltitudeMslMeters, double? AltitudeLocalMeters, double? AltitudeRelativeMeters, double? BottomClearanceMeters, DateTimeOffset? ObservedAt)
{
    /// <summary>Gets empty environment state.</summary>
    public static VehicleEnvironmentState Empty { get; } = new(null, null, null, null, null, null, null, null, null, null, null, null, null);

    /// <summary>Returns whether environment telemetry is stale.</summary>
    public bool IsStale(DateTimeOffset now, TimeSpan maximumAge) => ObservedAt is null || now - ObservedAt > maximumAge;
}
