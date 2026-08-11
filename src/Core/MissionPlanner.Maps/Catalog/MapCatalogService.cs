namespace MissionPlanner.Maps.Catalog;

/// <summary>Provides the immutable map catalog used by runtime services.</summary>
public interface IMapCatalog
{
    /// <summary>Gets the current catalog.</summary>
    MapCatalog Current { get; }
}

/// <summary>Provides the catalog embedded in <c>MissionPlanner.Maps</c>.</summary>
public sealed class BuiltInMapCatalogService : IMapCatalog
{
    /// <inheritdoc />
    public MapCatalog Current { get; } = BuiltInMapCatalog.Load();
}
