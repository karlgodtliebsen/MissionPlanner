using Mapsui.Layers;
using MissionPlanner.Maps.Sources;

namespace MissionPlanner.App.Maps;

/// <summary>Platform adapter for rendering installed MBTiles archives.</summary>
public interface IMapsuiMbTilesSourceFactory
{
    /// <summary>Creates the map layer for an installed MBTiles archive.</summary>
    ILayer Create(ResolvedMapSource source);
}

/// <summary>Fallback for platforms without an offline archive adapter.</summary>
public sealed class UnsupportedMapsuiMbTilesSourceFactory : IMapsuiMbTilesSourceFactory
{
    /// <summary>Always throws because offline MBTiles maps are not available on this platform.</summary>
    public ILayer Create(ResolvedMapSource source) =>
        throw new NotSupportedException("Offline MBTiles maps are not available on this platform. Select an online map source.");
}
