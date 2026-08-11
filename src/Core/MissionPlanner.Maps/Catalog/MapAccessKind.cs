namespace MissionPlanner.Maps.Catalog;

/// <summary>Identifies how a map source is accessed.</summary>
public enum MapAccessKind
{
    /// <summary>An HTTP XYZ tile endpoint.</summary>
    HttpXyz,

    /// <summary>An HTTP TMS tile endpoint.</summary>
    HttpTms,

    /// <summary>A Web Map Tile Service endpoint.</summary>
    Wmts,

    /// <summary>A Web Map Service endpoint.</summary>
    Wms,

    /// <summary>A locally installed archive.</summary>
    LocalArchive,

    /// <summary>A locally installed tile directory.</summary>
    LocalDirectory,

    /// <summary>An intentionally blank map.</summary>
    Blank
}
