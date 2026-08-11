namespace MissionPlanner.Maps.Sources;

/// <summary>Identifies where a resolved map source was defined.</summary>
public enum MapSourceOrigin
{
    /// <summary>The source is part of the reviewed application catalog.</summary>
    Catalog,

    /// <summary>The source is an installed offline pack.</summary>
    InstalledPack,

    /// <summary>The source is configured by the user.</summary>
    Custom
}
