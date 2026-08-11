using FluentAssertions;
using MissionPlanner.Maps.Attribution;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Sources;

namespace MissionPlanner.Core.Tests.Maps;

/// <summary>Verifies live attribution coordination across basemap changes.</summary>
public sealed class MapAttributionCoordinatorTests
{
    /// <summary>Verifies switching sources replaces the visible attribution and retains required fallback.</summary>
    [Fact]
    public async Task SourceSwitchUpdatesVisibleAttribution()
    {
        var coordinator = new MapAttributionCoordinator(new MapAttributionService(), new EmptyDynamicResolver());
        await coordinator.SetBasemapAsync(Source("osm-standard"), TestContext.Current.CancellationToken);
        coordinator.Current.DisplayText.Should().Contain("OpenStreetMap");

        await coordinator.SetBasemapAsync(Source("esri-world-topo"), TestContext.Current.CancellationToken);
        coordinator.Current.DisplayText.Should().Contain("Esri");
        coordinator.Current.Snapshot.OnExport.Should().NotBeEmpty();
    }

    /// <summary>Verifies compact/expanded state survives repeated refresh and is observable.</summary>
    [Fact]
    public async Task ToggleAndRefreshRemainStable()
    {
        var coordinator = new MapAttributionCoordinator(new MapAttributionService(), new ExtraDynamicResolver());
        var changes = 0;
        coordinator.Changed += (_, _) => changes++;
        await coordinator.SetBasemapAsync(Source("osm-standard"), TestContext.Current.CancellationToken);
        coordinator.ToggleExpanded();
        await coordinator.RefreshAsync(TestContext.Current.CancellationToken);

        coordinator.Current.IsExpanded.Should().BeTrue();
        coordinator.Current.DisplayText.Should().Contain(Environment.NewLine);
        changes.Should().Be(3);
    }

    private static ResolvedMapSource Source(string sourceId)
    {
        var catalog = BuiltInMapCatalog.Load();
        var definition = catalog.Sources.Single(item => item.Id == sourceId);
        var product = catalog.Products.Single(item => item.Id == definition.ProductId);
        return new(sourceId, MapSourceOrigin.Catalog, catalog.Providers.Single(item => item.Id == product.ProviderId), product, definition, catalog.Policies.Single(item => item.Id == definition.PolicyId), definition.AttributionIds.Select(id => catalog.Attributions.Single(item => item.Id == id)).ToArray(), new(MapCredentialRequirement.None, true), definition.UriTemplate);
    }

    private sealed class EmptyDynamicResolver : IMapDynamicAttributionResolver
    {
        public ValueTask<IReadOnlyCollection<MapAttributionEntry>> ResolveAsync(string contributorId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyCollection<MapAttributionEntry>>([]);
    }

    private sealed class ExtraDynamicResolver : IMapDynamicAttributionResolver
    {
        public ValueTask<IReadOnlyCollection<MapAttributionEntry>> ResolveAsync(string contributorId, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyCollection<MapAttributionEntry>>([new("dynamic", "Dynamic provider detail", null, true, true)]);
    }
}
