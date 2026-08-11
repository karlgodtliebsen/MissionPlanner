using System.Reflection;

namespace MissionPlanner.Maps.Catalog;

/// <summary>Loads the immutable catalog shipped with Mission Planner.</summary>
public static class BuiltInMapCatalog
{
    private const string ResourceName = "MissionPlanner.Maps.Resources.Maps.builtin-map-catalog.json";

    /// <summary>Loads and validates the embedded built-in catalog.</summary>
    /// <returns>The built-in catalog.</returns>
    public static MapCatalog Load()
    {
        using var stream = typeof(BuiltInMapCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded map catalog '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return MapCatalogSerializer.Deserialize(reader.ReadToEnd());
    }
}
