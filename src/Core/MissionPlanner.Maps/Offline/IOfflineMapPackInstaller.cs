namespace MissionPlanner.Maps.Offline;

/// <summary>Installs a validated pack using staging and atomic promotion.</summary>
public interface IOfflineMapPackInstaller
{
    /// <summary>Installs an archive stream for a manifest.</summary>
    ValueTask<InstalledOfflineMapPack> InstallAsync(OfflineMapPackManifest manifest, Stream archive, CancellationToken cancellationToken = default);
}
