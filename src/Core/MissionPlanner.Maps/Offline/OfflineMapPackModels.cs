namespace MissionPlanner.Maps.Offline;

/// <summary>Describes the geographic coverage of an offline map pack.</summary>
/// <param name="West">Western longitude.</param>
/// <param name="South">Southern latitude.</param>
/// <param name="East">Eastern longitude.</param>
/// <param name="North">Northern latitude.</param>
public sealed record OfflineMapBounds(double West, double South, double East, double North);

/// <summary>Describes an installable raster MBTiles pack.</summary>
/// <param name="Id">Stable pack identifier.</param>
/// <param name="Version">Pack version.</param>
/// <param name="DisplayName">User-facing name.</param>
/// <param name="ArchiveFileName">Archive file name without directories.</param>
/// <param name="Sha256">Lower- or upper-case hexadecimal SHA-256.</param>
/// <param name="SizeBytes">Expected archive size.</param>
/// <param name="Bounds">Declared WGS84 coverage.</param>
/// <param name="MinimumZoom">Minimum zoom.</param>
/// <param name="MaximumZoom">Maximum zoom.</param>
/// <param name="Projection">Expected projection, currently EPSG:3857.</param>
/// <param name="RasterFormat">Declared png, jpg, jpeg, or webp payload.</param>
/// <param name="Attribution">Required attribution text.</param>
/// <param name="LicenseNotice">License or rights notice.</param>
public sealed record OfflineMapPackManifest(
    string Id,
    string Version,
    string DisplayName,
    string ArchiveFileName,
    string Sha256,
    long SizeBytes,
    OfflineMapBounds Bounds,
    int MinimumZoom,
    int MaximumZoom,
    string Projection,
    string RasterFormat,
    string Attribution,
    string LicenseNotice);

/// <summary>Describes an installed offline map pack.</summary>
/// <param name="Manifest">Validated pack manifest.</param>
/// <param name="DirectoryPath">Version installation directory.</param>
/// <param name="ArchivePath">Read-only MBTiles archive path.</param>
public sealed record InstalledOfflineMapPack(OfflineMapPackManifest Manifest, string DirectoryPath, string ArchivePath);

/// <summary>Validates manifests and raster MBTiles archives.</summary>
public interface IOfflineMapPackValidator
{
    /// <summary>Validates a manifest and archive.</summary>
    ValueTask ValidateAsync(OfflineMapPackManifest manifest, string archivePath, CancellationToken cancellationToken = default);
}

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

/// <summary>Installs a validated pack using staging and atomic promotion.</summary>
public interface IOfflineMapPackInstaller
{
    /// <summary>Installs an archive stream for a manifest.</summary>
    ValueTask<InstalledOfflineMapPack> InstallAsync(OfflineMapPackManifest manifest, Stream archive, CancellationToken cancellationToken = default);
}
