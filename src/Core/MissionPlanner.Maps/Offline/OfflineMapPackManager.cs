namespace MissionPlanner.Maps.Offline;

/// <summary>
/// Default active-source-aware offline pack manager.
/// </summary>
public sealed class OfflineMapPackManager(IOfflineMapPackInstaller installer, IOfflineMapPackRepository repository, IActiveMapSourceStore activeSource) : IOfflineMapPackManager
{
    /// <inheritdoc />
    public ValueTask<InstalledOfflineMapPack> InstallAsync(OfflineMapPackManifest manifest, Stream archive, CancellationToken cancellationToken = default)
    {
        return installer.InstallAsync(manifest with { InstallOrigin = manifest.InstallOrigin == OfflineMapPackInstallOrigin.LegacyUnknown ? OfflineMapPackInstallOrigin.UserImported : manifest.InstallOrigin }, archive, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RemoveAsync(string id, string version, CancellationToken cancellationToken = default)
    {
        return StringComparer.Ordinal.Equals(activeSource.SelectedSourceId, $"pack:{id}:{version}")
            ? throw new InvalidOperationException("The active offline map pack cannot be removed. Select another basemap first.")
            : repository.RemoveAsync(id, version, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask ForceRemoveAsync(string id, string version, string fallbackSourceId = "osm-standard", CancellationToken cancellationToken = default)
    {
        if (StringComparer.Ordinal.Equals(activeSource.SelectedSourceId, $"pack:{id}:{version}"))
        {
            await activeSource.SetSelectedSourceIdAsync(fallbackSourceId, cancellationToken).ConfigureAwait(false);
        }

        await repository.RemoveAsync(id, version, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
