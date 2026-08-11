using MissionPlanner.Maps.Catalog;

namespace MissionPlanner.Maps.Attribution;

/// <summary>Contributes attribution for a currently visible source or layer.</summary>
public interface IMapAttributionContributor
{
    /// <summary>Gets a stable contributor identifier.</summary>
    string ContributorId { get; }

    /// <summary>Gets whether this contributor is currently visible.</summary>
    bool IsVisible { get; }

    /// <summary>Gets static attribution entries.</summary>
    IReadOnlyCollection<MapAttributionEntry> Attributions { get; }
}

/// <summary>Resolves service-provided attribution that can vary by viewport or response.</summary>
public interface IMapDynamicAttributionResolver
{
    /// <summary>Resolves attribution for a visible contributor.</summary>
    /// <param name="contributorId">Stable contributor identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dynamic attribution entries.</returns>
    ValueTask<IReadOnlyCollection<MapAttributionEntry>> ResolveAsync(string contributorId, CancellationToken cancellationToken = default);
}

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

/// <summary>Contains deduplicated attribution for display and export.</summary>
/// <param name="Entries">All current attribution entries.</param>
public sealed record MapAttributionSnapshot(IReadOnlyList<MapAttributionEntry> Entries)
{
    /// <summary>Gets entries required on the interactive map.</summary>
    public IReadOnlyList<MapAttributionEntry> OnMap => Entries.Where(item => item.RequiredOnMap).ToArray();

    /// <summary>Gets entries required in exported output.</summary>
    public IReadOnlyList<MapAttributionEntry> OnExport => Entries.Where(item => item.RequiredOnExport).ToArray();

    /// <summary>Gets compact display text.</summary>
    public string CompactText => string.Join(" · ", OnMap.Select(item => item.Text));

    /// <summary>Gets expanded display text.</summary>
    public string ExpandedText => string.Join(Environment.NewLine, OnMap.Select(item => item.Text));
}

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
                entries.TryAdd(entry.Id, entry);
            if (dynamicResolver is null)
                continue;
            foreach (var entry in await dynamicResolver.ResolveAsync(contributor.ContributorId, cancellationToken).ConfigureAwait(false))
                entries.TryAdd(entry.Id, entry);
        }

        return new(entries.Values.OrderBy(item => item.Id, StringComparer.Ordinal).ToArray());
    }
}
