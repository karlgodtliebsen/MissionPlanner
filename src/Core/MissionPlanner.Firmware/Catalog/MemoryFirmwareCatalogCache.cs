namespace MissionPlanner.Firmware.Catalog;

/// <summary>Provides the default process-local catalogue cache.</summary>
public sealed class MemoryFirmwareCatalogCache : IFirmwareCatalogCache
{
    private CachedFirmwareManifest? value;

    /// <inheritdoc />
    public Task<CachedFirmwareManifest?> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(value);
    }

    /// <inheritdoc />
    public Task SetAsync(CachedFirmwareManifest manifest, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        value = manifest with { Content = manifest.Content.ToArray() };
        return Task.CompletedTask;
    }
}
