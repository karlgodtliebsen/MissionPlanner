using MissionPlanner.Maps.Catalog;

namespace MissionPlanner.Maps.Attribution;

/// <summary>Default attribution aggregation service.</summary>
public sealed class MapAttributionService : IMapAttributionService
{
    /// <inheritdoc />
    public async ValueTask<MapAttributionSnapshot> GetCurrentAsync(
        IEnumerable<IMapAttributionContributor> contributors,
        IMapDynamicAttributionResolver? dynamicResolver = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contributors);
        var entries = new Dictionary<string, MapAttributionEntry>(StringComparer.Ordinal);
        foreach (var contributor in contributors.Where(item => item.IsVisible))
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var entry in contributor.Attributions)
            {
                entries.TryAdd(entry.Id, entry);
            }

            if (dynamicResolver is null)
            {
                continue;
            }

            foreach (var entry in await dynamicResolver.ResolveAsync(contributor.ContributorId, cancellationToken).ConfigureAwait(false))
            {
                entries.TryAdd(entry.Id, entry);
            }
        }

        return new MapAttributionSnapshot(entries.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray());
    }
}
