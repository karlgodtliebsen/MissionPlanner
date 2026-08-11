using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Offline;

namespace MissionPlanner.Maps.Diagnostics;

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
        return new MapDiagnosticSnapshot(
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
