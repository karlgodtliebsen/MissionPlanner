namespace MissionPlanner.Maps.Offline;

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
