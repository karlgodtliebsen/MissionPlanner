namespace MissionPlanner.Firmware.Catalog;

/// <summary>Persists source manifest bytes independently of catalogue logic.</summary>
public interface IFirmwareCatalogCache
{
    /// <summary>Reads the last valid cached manifest.</summary>
    Task<CachedFirmwareManifest?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores a validated manifest.</summary>
    Task SetAsync(CachedFirmwareManifest manifest, CancellationToken cancellationToken = default);
}
