namespace MissionPlanner.Firmware.Catalog;

/// <summary>Retrieves manifest bytes from a remote source.</summary>
public interface IFirmwareManifestClient
{
    /// <summary>Gets a manifest, optionally using cached HTTP validators.</summary>
    Task<FirmwareManifestResponse> GetAsync(Uri uri, CachedFirmwareManifest? cached, CancellationToken cancellationToken = default);
}
