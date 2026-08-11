namespace MissionPlanner.Maps.Terrain;

/// <summary>Identifies the presentation state of an arbitrary-coordinate terrain lookup.</summary>
public enum TerrainElevationStatus
{
    /// <summary>No lookup has started.</summary>
    Idle,
    /// <summary>The debounce or terrain lookup is in progress.</summary>
    Loading,
    /// <summary>A valid terrain elevation is available.</summary>
    Available,
    /// <summary>The coordinate is outside available SRTM terrain coverage.</summary>
    OutsideCoverage,
    /// <summary>The terrain source could not be reached.</summary>
    NetworkUnavailable,
    /// <summary>The downloaded or cached terrain data is invalid.</summary>
    InvalidData
}

/// <summary>Contains a typed terrain lookup result.</summary>
/// <param name="Status">Lookup status.</param>
/// <param name="ElevationMeters">Elevation above mean sea level when available.</param>
/// <param name="TileId">SRTM tile identifier.</param>
/// <param name="Message">Optional diagnostic-safe explanation.</param>
public sealed record TerrainElevationResult(TerrainElevationStatus Status, double? ElevationMeters, string? TileId, string? Message = null);

/// <summary>Looks up terrain elevation at an arbitrary geographic coordinate.</summary>
public interface ITerrainElevationService
{
    /// <summary>Gets a typed terrain elevation result for the coordinate.</summary>
    ValueTask<TerrainElevationResult> GetElevationAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}
