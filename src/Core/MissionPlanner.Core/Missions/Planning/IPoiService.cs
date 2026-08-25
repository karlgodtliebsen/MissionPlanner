using MissionPlanner.Core.Missions.Models;

namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Owns validated local POIs.</summary>
public interface IPoiService
{
    /// <summary>Raised after local state changes.</summary>
    event Action? Changed;
    /// <summary>Gets current state.</summary>
    PoiSnapshot Snapshot
    {
        get;
    }
    /// <summary>Loads persistent state once.</summary>
    Task ActivateAsync(CancellationToken cancellationToken = default);
    /// <summary>Adds a POI.</summary>
    Task<PointOfInterest> AddAsync(string name, GeoPosition position, double? altitude, string? description, string? category, CancellationToken cancellationToken = default);
    /// <summary>Updates a POI.</summary>
    Task UpdateAsync(PointOfInterest item, CancellationToken cancellationToken = default);
    /// <summary>Deletes a POI.</summary>
    Task DeleteAsync(PointOfInterestId id, CancellationToken cancellationToken = default);
    /// <summary>Finds the closest POI to a geographic target.</summary>
    PointOfInterest? FindNearest(GeoPosition position);
}
