namespace MissionPlanner.Maps.Catalog;

/// <summary>Contains versioned map provider, product, policy, attribution, and source definitions.</summary>
/// <param name="SchemaVersion">Catalog schema version.</param>
/// <param name="CatalogVersion">Catalog content version.</param>
/// <param name="Providers">Provider definitions.</param>
/// <param name="Products">Product definitions.</param>
/// <param name="Policies">Usage policies.</param>
/// <param name="Attributions">Attribution entries.</param>
/// <param name="Sources">Map source definitions.</param>
public sealed record MapCatalog(
    int SchemaVersion,
    string CatalogVersion,
    MapProviderDefinition[] Providers,
    MapDataProductDefinition[] Products,
    MapUsagePolicy[] Policies,
    MapAttributionEntry[] Attributions,
    MapSourceDefinition[] Sources);
