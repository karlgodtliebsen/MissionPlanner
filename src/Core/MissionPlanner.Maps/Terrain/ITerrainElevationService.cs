namespace MissionPlanner.Maps.Terrain;

/// <summary>
/// Looks up terrain elevation at an arbitrary geographic coordinate.
/// </summary>
public interface ITerrainElevationService
{
    /// <summary>
    /// Gets terrain elevation above mean sea level in metres, or <see langword="null"/> when unavailable.
    /// </summary>
    ValueTask<double?> GetElevationMetersAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}
