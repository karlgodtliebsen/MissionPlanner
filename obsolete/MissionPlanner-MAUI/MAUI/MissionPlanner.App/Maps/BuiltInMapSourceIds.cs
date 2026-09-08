namespace MissionPlanner.App.Maps;

/// <summary>Maps legacy mission-editor display values to stable catalog source identifiers.</summary>
public static class BuiltInMapSourceIds
{
    /// <summary>Resolves a legacy display name or stable identifier.</summary>
    public static string Resolve(string value) => value switch
    {
        "OpenStreetMap" => "osm-standard",
        "Esri World Topo" => "esri-world-topo",
        "Esri World Physical" => "esri-world-physical",
        "Esri Shaded Relief" => "esri-world-shaded-relief",
        "Esri Dark Gray" => "esri-world-dark-gray",
        "No Map" => "no-map",
        _ => value
    };
}
