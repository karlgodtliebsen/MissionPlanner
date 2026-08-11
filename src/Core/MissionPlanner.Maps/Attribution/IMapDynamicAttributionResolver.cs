using MissionPlanner.Maps.Catalog;

namespace MissionPlanner.Maps.Attribution;

/// <summary>Resolves service-provided attribution that can vary by viewport or response.</summary>
public interface IMapDynamicAttributionResolver
{
    /// <summary>Resolves attribution for a visible contributor.</summary>
    /// <param name="contributorId">Stable contributor identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dynamic attribution entries.</returns>
    ValueTask<IReadOnlyCollection<MapAttributionEntry>> ResolveAsync(string contributorId, CancellationToken cancellationToken = default);
}
