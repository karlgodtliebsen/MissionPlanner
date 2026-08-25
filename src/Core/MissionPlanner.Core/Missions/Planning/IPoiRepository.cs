namespace MissionPlanner.Core.Missions.Planning;

/// <summary>Persistent POI storage boundary.</summary>
public interface IPoiRepository
{
    /// <summary>Loads persisted POIs.</summary>
    Task<IReadOnlyList<PointOfInterest>> LoadAsync(CancellationToken cancellationToken = default);
    /// <summary>Atomically saves POIs.</summary>
    Task SaveAsync(IReadOnlyList<PointOfInterest> items, CancellationToken cancellationToken = default);
}