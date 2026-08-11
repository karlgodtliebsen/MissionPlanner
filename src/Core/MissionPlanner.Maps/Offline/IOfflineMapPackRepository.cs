namespace MissionPlanner.Maps.Offline;

/// <summary>Lists and removes installed packs.</summary>
public interface IOfflineMapPackRepository
{
    /// <summary>Lists installed packs.</summary>
    ValueTask<IReadOnlyList<InstalledOfflineMapPack>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Finds an installed pack version.</summary>
    ValueTask<InstalledOfflineMapPack?> FindAsync(string id, string version, CancellationToken cancellationToken = default);

    /// <summary>Removes a pack unless it is active.</summary>
    ValueTask RemoveAsync(string id, string version, string? activePackId = null, CancellationToken cancellationToken = default);
}
