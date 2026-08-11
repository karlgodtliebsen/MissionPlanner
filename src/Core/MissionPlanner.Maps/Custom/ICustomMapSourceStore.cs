namespace MissionPlanner.Maps.Custom;

/// <summary>Persists non-secret custom source settings.</summary>
public interface ICustomMapSourceStore
{
    /// <summary>Loads configured sources.</summary>
    ValueTask<IReadOnlyList<CustomMapSourceSettings>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Saves configured sources atomically.</summary>
    ValueTask SaveAsync(IReadOnlyList<CustomMapSourceSettings> sources, CancellationToken cancellationToken = default);
}
