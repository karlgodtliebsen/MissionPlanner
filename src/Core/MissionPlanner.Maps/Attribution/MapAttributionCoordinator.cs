using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Sources;

namespace MissionPlanner.Maps.Attribution;

/// <summary>Default deduplicating attribution coordinator.</summary>
public sealed class MapAttributionCoordinator(
    IMapAttributionService attributionService,
    IMapDynamicAttributionResolver dynamicResolver) : IMapAttributionCoordinator
{
    private ResolvedMapSource? currentSource;

    /// <inheritdoc />
    public MapAttributionOverlayState Current { get; private set; } = new();

    /// <inheritdoc />
    public event EventHandler<MapAttributionOverlayState>? Changed;

    /// <inheritdoc />
    public async ValueTask SetBasemapAsync(ResolvedMapSource? source, CancellationToken cancellationToken = default)
    {
        currentSource = source;
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        var wasExpanded = Current.IsExpanded;
        if (currentSource is null)
        {
            Publish(new MapAttributionOverlayState(new MapAttributionSnapshot([]), wasExpanded));
            return;
        }

        var contributor = new SourceContributor(currentSource);
        var snapshot = await attributionService.GetCurrentAsync([contributor], dynamicResolver, cancellationToken).ConfigureAwait(false);
        // Static reviewed entries are always retained, so mandatory attribution never disappears
        // when provider metadata is unavailable.
        Publish(new MapAttributionOverlayState(snapshot, wasExpanded, currentSource.EffectivePolicy.RequiresVisibleAttribution && snapshot.OnMap.Count == 0));
    }

    /// <inheritdoc />
    public void ToggleExpanded()
    {
        Current.IsExpanded = !Current.IsExpanded;
        Changed?.Invoke(this, Current);
    }

    private void Publish(MapAttributionOverlayState state)
    {
        Current = state;
        Changed?.Invoke(this, state);
    }

    private sealed record SourceContributor(ResolvedMapSource Source) : IMapAttributionContributor
    {
        public string ContributorId => Source.Id;
        public bool IsVisible => true;
        public IReadOnlyCollection<MapAttributionEntry> Attributions => Source.Attribution;
    }
}
