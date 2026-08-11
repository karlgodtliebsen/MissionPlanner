namespace MissionPlanner.Maps.Attribution;

/// <summary>Aggregates attribution from visible map sources and layers.</summary>
public interface IMapAttributionService
{
    /// <summary>Builds a deduplicated attribution snapshot.</summary>
    /// <param name="contributors">Potential contributors.</param>
    /// <param name="dynamicResolver">Optional dynamic attribution resolver.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current attribution snapshot.</returns>
    ValueTask<MapAttributionSnapshot> GetCurrentAsync(
        IEnumerable<IMapAttributionContributor> contributors,
        IMapDynamicAttributionResolver? dynamicResolver = null,
        CancellationToken cancellationToken = default);
}
