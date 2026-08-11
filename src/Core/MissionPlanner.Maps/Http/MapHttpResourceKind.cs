namespace MissionPlanner.Maps.Http;

/// <summary>Identifies the kind of online map resource being fetched.</summary>
public enum MapHttpResourceKind
{
    /// <summary>A raster tile.</summary>
    RasterTile,

    /// <summary>Provider service metadata.</summary>
    ProviderMetadata,

    /// <summary>Dynamic attribution metadata.</summary>
    AttributionMetadata,

    /// <summary>WMS capabilities XML.</summary>
    WmsCapabilities,

    /// <summary>WMTS capabilities XML.</summary>
    WmtsCapabilities,

    /// <summary>Style metadata.</summary>
    StyleMetadata
}
