using System.Text.Json;
using MissionPlanner.Maps.Attribution;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Offline;

namespace MissionPlanner.Maps.Diagnostics;

/// <summary>Contains sanitized, support-safe map subsystem diagnostics.</summary>
/// <param name="SelectedSourceId">Selected stable source identifier.</param>
/// <param name="ProviderId">Provider identifier.</param>
/// <param name="ProductId">Data-product identifier.</param>
/// <param name="SourceKind">Access kind.</param>
/// <param name="ArchiveFormat">Archive format.</param>
/// <param name="PayloadFormat">Tile payload format.</param>
/// <param name="Connectivity">Online/offline state.</param>
/// <param name="CredentialConfigured">Whether the required credential is configured.</param>
/// <param name="PolicyId">Reviewed policy identifier.</param>
/// <param name="PolicyReviewedOn">Policy review date.</param>
/// <param name="AttributionIds">Required attribution identifiers.</param>
/// <param name="CacheSizeBytes">Current HTTP cache size.</param>
/// <param name="ActivePackId">Active pack identifier.</param>
/// <param name="ActivePackVersion">Active pack version.</param>
/// <param name="MapsuiVersion">Mapsui assembly version.</param>
/// <param name="Platform">Runtime platform.</param>
/// <param name="LastSourceError">Sanitized last source error.</param>
public sealed record MapDiagnosticSnapshot(
    string SelectedSourceId,
    string ProviderId,
    string ProductId,
    MapAccessKind SourceKind,
    MapArchiveFormat ArchiveFormat,
    MapTileContentFormat PayloadFormat,
    string Connectivity,
    bool CredentialConfigured,
    string PolicyId,
    DateOnly PolicyReviewedOn,
    IReadOnlyList<string> AttributionIds,
    long CacheSizeBytes,
    string? ActivePackId,
    string? ActivePackVersion,
    string MapsuiVersion,
    string Platform,
    string? LastSourceError)
{
    /// <summary>Serializes the sanitized snapshot for support diagnostics.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
}

/// <summary>Builds sanitized map diagnostics from catalog and runtime state.</summary>
public static class MapDiagnosticSnapshotFactory
{
    /// <summary>Creates a support-safe diagnostic snapshot.</summary>
    public static MapDiagnosticSnapshot Create(
        MapCatalog catalog,
        MapSourceDefinition source,
        bool credentialConfigured,
        bool isOnline,
        long cacheSizeBytes,
        InstalledOfflineMapPack? activePack,
        string mapsuiVersion,
        string platform,
        string? lastSourceError,
        string? knownSecret = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(source);
        var product = catalog.Products.Single(value => value.Id == source.ProductId);
        var policy = catalog.Policies.Single(value => value.Id == source.PolicyId);
        return new(
            source.Id,
            product.ProviderId,
            product.Id,
            source.AccessKind,
            source.ArchiveFormat,
            source.ContentFormat,
            isOnline ? "Online" : "Offline",
            credentialConfigured,
            policy.Id,
            policy.ReviewedOn,
            source.AttributionIds.ToArray(),
            Math.Max(0, cacheSizeBytes),
            activePack?.Manifest.Id,
            activePack?.Manifest.Version,
            mapsuiVersion,
            platform,
            lastSourceError is null ? null : MapDiagnosticRedactor.Redact(lastSourceError, knownSecret));
    }
}

/// <summary>Provides required attribution to future screenshot, static-image, and PDF exporters.</summary>
public static class MapExportAttribution
{
    /// <summary>Builds a footer from entries that require attribution in exported output.</summary>
    public static string CreateFooter(MapAttributionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return string.Join(" · ", snapshot.OnExport.Select(value => value.Text).Distinct(StringComparer.Ordinal));
    }
}
