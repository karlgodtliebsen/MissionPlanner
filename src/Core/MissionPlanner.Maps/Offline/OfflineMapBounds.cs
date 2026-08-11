namespace MissionPlanner.Maps.Offline;

/// <summary>
/// Describes the geographic coverage of an offline map pack.
/// </summary>
/// <param name="West">Western longitude.</param>
/// <param name="South">Southern latitude.</param>
/// <param name="East">Eastern longitude.</param>
/// <param name="North">Northern latitude.</param>
public sealed record OfflineMapBounds(double West, double South, double East, double North);
