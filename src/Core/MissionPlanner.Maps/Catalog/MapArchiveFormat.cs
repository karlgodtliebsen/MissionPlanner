namespace MissionPlanner.Maps.Catalog;

/// <summary>Identifies an archive container format.</summary>
public enum MapArchiveFormat
{
    /// <summary>No archive is used.</summary>
    None,

    /// <summary>An MBTiles SQLite archive.</summary>
    MbTiles,

    /// <summary>A PMTiles archive.</summary>
    PmTiles
}
