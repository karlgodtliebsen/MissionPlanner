namespace MissionPlanner.Maps.Offline;

/// <summary>Validates manifests and raster MBTiles archives.</summary>
public interface IOfflineMapPackValidator
{
    /// <summary>Validates a manifest and archive.</summary>
    ValueTask ValidateAsync(OfflineMapPackManifest manifest, string archivePath, CancellationToken cancellationToken = default);
}
