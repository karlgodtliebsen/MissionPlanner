using MissionPlanner.Maps.Catalog;

namespace MissionPlanner.Maps.Settings;

/// <summary>Identifies the user-facing group in which a map source is displayed.</summary>
public enum MapSettingsSourceGroup
{
    /// <summary>An installed offline pack.</summary>
    OfflinePacks,
    /// <summary>A source controlled by the operator.</summary>
    SelfHostedOrCustom,
    /// <summary>A hosted online provider.</summary>
    OnlineProviders,
    /// <summary>The intentionally blank basemap.</summary>
    BlankMap
}

/// <summary>Provides settings-page metadata for one selectable map source.</summary>
/// <param name="Source">Catalog source definition.</param>
/// <param name="Group">User-facing source group.</param>
/// <param name="ProviderAndProduct">Provider and data-product display text.</param>
/// <param name="Connectivity">Online or offline description.</param>
/// <param name="Rendering">Raster or vector description.</param>
/// <param name="CredentialState">Credential requirement and configured state.</param>
/// <param name="AttributionPreview">Required attribution preview.</param>
/// <param name="CacheBehavior">Allowed cache behavior.</param>
/// <param name="OfflinePackAvailability">Offline-pack availability.</param>
/// <param name="PolicyReviewDate">Policy review date.</param>
/// <param name="TermsUri">Terms or source-details URI.</param>
public sealed record MapSettingsSourceItem(
    MapSourceDefinition Source,
    MapSettingsSourceGroup Group,
    string ProviderAndProduct,
    string Connectivity,
    string Rendering,
    string CredentialState,
    string AttributionPreview,
    string CacheBehavior,
    string OfflinePackAvailability,
    DateOnly PolicyReviewDate,
    Uri? TermsUri)
{
    /// <summary>Gets the user-facing source name.</summary>
    public string DisplayName => Source.DisplayName;

    /// <summary>Gets the stable source identifier.</summary>
    public string Id => Source.Id;
}

/// <summary>Builds source settings metadata and resolves persisted selections safely.</summary>
public static class MapSettingsSourceCatalog
{
    /// <summary>Builds user-facing items from a validated map catalog.</summary>
    public static IReadOnlyList<MapSettingsSourceItem> Create(MapCatalog catalog, IReadOnlySet<string>? configuredCredentialSourceIds = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        configuredCredentialSourceIds ??= new HashSet<string>(StringComparer.Ordinal);
        return catalog.Sources.Where(source => !source.IsFutureCandidate).Select(source =>
        {
            var product = catalog.Products.Single(value => value.Id == source.ProductId);
            var provider = catalog.Providers.Single(value => value.Id == product.ProviderId);
            var policy = catalog.Policies.Single(value => value.Id == source.PolicyId);
            var attribution = string.Join(" · ", catalog.Attributions.Where(value => source.AttributionIds.Contains(value.Id)).Select(value => value.Text));
            var configured = source.CredentialRequirement == MapCredentialRequirement.None || configuredCredentialSourceIds.Contains(source.Id);
            return new MapSettingsSourceItem(
                source,
                Classify(source),
                $"{provider.DisplayName} — {product.DisplayName}",
                source.AccessKind is MapAccessKind.LocalArchive or MapAccessKind.LocalDirectory ? "Offline" : source.AccessKind == MapAccessKind.Blank ? "No network" : "Online",
                source.ContentFormat == MapTileContentFormat.VectorMvt ? "Vector" : "Raster",
                source.CredentialRequirement == MapCredentialRequirement.None ? "No credential required" : $"{source.CredentialRequirement}: {(configured ? "configured" : "not configured")}",
                string.IsNullOrWhiteSpace(attribution) ? "No provider attribution" : attribution,
                policy.AllowOfflineCache && source.Capabilities.SupportsOfflineCache ? "Bounded HTTP cache allowed" : "No HTTP cache",
                policy.AllowPackDownload && source.Capabilities.SupportsPackDownload ? "Pack workflow available" : "Pack download unavailable",
                policy.ReviewedOn,
                policy.TermsUri);
        }).OrderBy(value => value.Group).ThenBy(value => value.DisplayName, StringComparer.CurrentCulture).ToArray();
    }

    /// <summary>Resolves a persisted source, falling back when it was deleted, is unavailable offline, or lacks credentials.</summary>
    public static MapSettingsSourceItem Resolve(
        IEnumerable<MapSettingsSourceItem> sources,
        string? selectedSourceId,
        bool isOnline,
        string fallbackSourceId = "osm-standard")
    {
        ArgumentNullException.ThrowIfNull(sources);
        var values = sources.ToArray();
        var selected = values.FirstOrDefault(value => StringComparer.Ordinal.Equals(value.Id, selectedSourceId));
        if (selected is not null && IsAvailable(selected, isOnline))
            return selected;
        return values.FirstOrDefault(value => StringComparer.Ordinal.Equals(value.Id, fallbackSourceId) && IsAvailable(value, isOnline))
               ?? values.FirstOrDefault(value => value.Group == MapSettingsSourceGroup.OfflinePacks)
               ?? values.First(value => value.Group == MapSettingsSourceGroup.BlankMap);
    }

    private static bool IsAvailable(MapSettingsSourceItem source, bool isOnline) =>
        (isOnline || source.Connectivity != "Online") && !source.CredentialState.EndsWith("not configured", StringComparison.Ordinal);

    private static MapSettingsSourceGroup Classify(MapSourceDefinition source) => source.AccessKind switch
    {
        MapAccessKind.Blank => MapSettingsSourceGroup.BlankMap,
        MapAccessKind.LocalArchive or MapAccessKind.LocalDirectory => MapSettingsSourceGroup.OfflinePacks,
        _ when source.PolicyId == "user-controlled-network-v1" => MapSettingsSourceGroup.SelfHostedOrCustom,
        _ => MapSettingsSourceGroup.OnlineProviders
    };
}
