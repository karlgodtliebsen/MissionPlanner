namespace MissionPlanner.Maps.Catalog;

/// <summary>Provides the catalog embedded in <c>MissionPlanner.Maps</c>.</summary>
public sealed class BuiltInMapCatalogService : IMapCatalog
{
    /// <inheritdoc />
    public MapCatalog Current { get; } = BuiltInMapCatalog.Load();
}
