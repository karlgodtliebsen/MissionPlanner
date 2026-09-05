using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Maps.Terrain;

namespace MissionPlanner.App.Presentation;

/// <summary>Adapts the shared terrain subsystem to mission profile sampling.</summary>
public sealed class MissionTerrainElevationProvider(ITerrainElevationService terrain) : IMissionTerrainElevationProvider
{
    /// <inheritdoc />
    public async ValueTask<double?> GetElevationAsync(GeoPosition position, CancellationToken cancellationToken = default)
    {
        var result = await terrain.GetElevationAsync(position.LatitudeDegrees, position.LongitudeDegrees, cancellationToken);
        return result.Status == TerrainElevationStatus.Available ? result.ElevationMeters : null;
    }
}
