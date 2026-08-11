namespace MissionPlanner.Maps.Offline;

/// <summary>Owns pack installation and removal relative to the active source.</summary>
public interface IOfflineMapPackManager
{
    /// <summary>Installs a user-imported pack through the common bounded primitive.</summary>
    ValueTask<InstalledOfflineMapPack> InstallAsync(OfflineMapPackManifest manifest, Stream archive, CancellationToken cancellationToken = default);

    /// <summary>Removes an inactive pack.</summary>
    ValueTask RemoveAsync(string id, string version, CancellationToken cancellationToken = default);

    /// <summary>Switches to fallback before explicitly removing an active pack.</summary>
    ValueTask ForceRemoveAsync(string id, string version, string fallbackSourceId = "osm-standard", CancellationToken cancellationToken = default);
}
