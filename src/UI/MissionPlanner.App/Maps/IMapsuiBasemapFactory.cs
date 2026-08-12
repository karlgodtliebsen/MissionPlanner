using MissionPlanner.Maps.Sources;

namespace MissionPlanner.App.Maps;

/// <summary>Creates Mapsui basemaps from renderer-neutral resolved sources.</summary>
public interface IMapsuiBasemapFactory
{
    /// <summary>Creates a basemap without performing source selection.</summary>
    ValueTask<MapBasemapCreationResult> CreateAsync(ResolvedMapSource source, CancellationToken cancellationToken = default);
}
