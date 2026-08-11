namespace MissionPlanner.Maps.Catalog;

/// <summary>Identifies the content stored by a map source.</summary>
public enum MapTileContentFormat
{
    /// <summary>PNG raster tiles.</summary>
    RasterPng,

    /// <summary>JPEG raster tiles.</summary>
    RasterJpeg,

    /// <summary>WebP raster tiles.</summary>
    RasterWebp,

    /// <summary>Mapbox vector tiles.</summary>
    VectorMvt
}
