namespace MissionPlanner.Maps.Catalog;

/// <summary>Provides the immutable map catalog used by runtime services.</summary>
public interface IMapCatalog
{
    /// <summary>Gets the current catalog.</summary>
    MapCatalog Current { get; }
}
