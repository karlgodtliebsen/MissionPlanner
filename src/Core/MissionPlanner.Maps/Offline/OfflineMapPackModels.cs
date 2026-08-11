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
/// <param name="SourceId">Reviewed catalog source identifier, when known.</param>
/// <param name="ProductId">Reviewed catalog product identifier, when known.</param>
/// <param name="PolicyId">Policy identifier evaluated for this installation.</param>
/// <param name="PolicyReviewedOn">Date on which that policy was reviewed.</param>
/// <param name="InstallOrigin">How the pack entered the repository.</param>
/// <param name="Provenance">Sanitized source provenance without credentials.</param>
/// <param name="RetrievedAt">Time at which the artifact was retrieved.</param>
/// <param name="AttributionIds">Attribution identifiers retained with the pack.</param>
/// <param name="NoticeReferences">Required notice references retained with the pack.</param>
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
    string LicenseNotice,
    string? SourceId = null,
    string? ProductId = null,
    string? PolicyId = null,
    DateOnly? PolicyReviewedOn = null,
    OfflineMapPackInstallOrigin InstallOrigin = OfflineMapPackInstallOrigin.LegacyUnknown,
    string? Provenance = null,
    DateTimeOffset? RetrievedAt = null,
    string[]? AttributionIds = null,
    string[]? NoticeReferences = null);

/// <summary>Identifies how an installed pack entered the repository.</summary>
public enum OfflineMapPackInstallOrigin
{
    /// <summary>An older manifest has no recorded provenance.</summary>
    LegacyUnknown,
    /// <summary>The operator imported a local archive.</summary>
    UserImported,
    /// <summary>The archive came from an approved signed feed.</summary>
    ApprovedFeed
}

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

/// <summary>Gets and changes the authoritative active map source ID.</summary>
public interface IActiveMapSourceStore
{
    /// <summary>Gets the active stable source identifier.</summary>
    string SelectedSourceId { get; }
    /// <summary>Changes the active stable source identifier.</summary>
    ValueTask SetSelectedSourceIdAsync(string sourceId, CancellationToken cancellationToken = default);
}

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

/// <summary>Default active-source-aware offline pack manager.</summary>
public sealed class OfflineMapPackManager(IOfflineMapPackInstaller installer, IOfflineMapPackRepository repository, IActiveMapSourceStore activeSource) : IOfflineMapPackManager
{
    /// <inheritdoc />
    public ValueTask<InstalledOfflineMapPack> InstallAsync(OfflineMapPackManifest manifest, Stream archive, CancellationToken cancellationToken = default) =>
        installer.InstallAsync(manifest with { InstallOrigin = manifest.InstallOrigin == OfflineMapPackInstallOrigin.LegacyUnknown ? OfflineMapPackInstallOrigin.UserImported : manifest.InstallOrigin }, archive, cancellationToken);

    /// <inheritdoc />
    public ValueTask RemoveAsync(string id, string version, CancellationToken cancellationToken = default)
    {
        if (StringComparer.Ordinal.Equals(activeSource.SelectedSourceId, $"pack:{id}:{version}"))
            throw new InvalidOperationException("The active offline map pack cannot be removed. Select another basemap first.");
        return repository.RemoveAsync(id, version, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask ForceRemoveAsync(string id, string version, string fallbackSourceId = "osm-standard", CancellationToken cancellationToken = default)
    {
        if (StringComparer.Ordinal.Equals(activeSource.SelectedSourceId, $"pack:{id}:{version}"))
            await activeSource.SetSelectedSourceIdAsync(fallbackSourceId, cancellationToken).ConfigureAwait(false);
        await repository.RemoveAsync(id, version, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
