using FluentAssertions;
using Mapsui;
using Mapsui.Layers;
using MissionPlanner.App.Maps;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Sources;

namespace MissionPlanner.Core.Tests.Maps;

public sealed class MapBasemapControllerTests
{
    [Fact]
    public async Task Switch_ReplacesOnlyBasemapAndPreservesOperationalLayersAndViewport()
    {
        var map = new Mapsui.Map();
        var mission = new MemoryLayer { Name = "Mission route" };
        var vehicle = new MemoryLayer { Name = "Vehicle marker" };
        map.Layers.Add(mission);
        map.Layers.Add(vehicle);
        map.Navigator.CenterOnAndZoomTo(new MPoint(123, 456), 25);
        var before = map.Navigator.Viewport;
        using var controller = new MapBasemapController(map, new FakeResolver(), new FakeFactory());

        (await controller.TrySwitchAsync("osm-standard", TestContext.Current.CancellationToken)).Should().BeTrue();
        (await controller.TrySwitchAsync("esri-world-topo", TestContext.Current.CancellationToken)).Should().BeTrue();

        map.Layers.Should().ContainSingle(layer => layer.Name == MapsuiBasemapFactory.BasemapLayerName);
        map.Layers.Should().Contain(layer => ReferenceEquals(layer, mission));
        map.Layers.Should().Contain(layer => ReferenceEquals(layer, vehicle));
        map.Navigator.Viewport.CenterX.Should().Be(before.CenterX);
        map.Navigator.Viewport.CenterY.Should().Be(before.CenterY);
        map.Navigator.Viewport.Resolution.Should().Be(before.Resolution);
    }

    [Fact]
    public async Task FailedSwitch_RetainsPreviousWorkingBasemap()
    {
        var map = new Mapsui.Map();
        using var controller = new MapBasemapController(map, new FakeResolver(), new FakeFactory());
        await controller.TrySwitchAsync("osm-standard", TestContext.Current.CancellationToken);
        var previous = map.Layers.Single();

        (await controller.TrySwitchAsync("fail", TestContext.Current.CancellationToken)).Should().BeFalse();

        map.Layers.Should().ContainSingle().Which.Should().BeSameAs(previous);
        controller.CurrentSourceId.Should().Be("osm-standard");
    }

    [Theory]
    [InlineData("OpenStreetMap", "osm-standard")]
    [InlineData("Esri World Topo", "esri-world-topo")]
    [InlineData("Esri World Physical", "esri-world-physical")]
    [InlineData("Esri Shaded Relief", "esri-world-shaded-relief")]
    [InlineData("Esri Dark Gray", "esri-world-dark-gray")]
    [InlineData("No Map", "no-map")]
    public void LegacySelection_MapsToStableCatalogIdentity(string displayName, string sourceId) =>
        BuiltInMapSourceIds.Resolve(displayName).Should().Be(sourceId);

    private sealed class FakeFactory : IMapsuiBasemapFactory
    {
        public ValueTask<MapBasemapCreationResult> CreateAsync(ResolvedMapSource source, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(source.Id == "fail"
                ? new MapBasemapCreationResult(MapBasemapCreationStatus.RendererFailure, null, "Expected failure.")
                : new MapBasemapCreationResult(MapBasemapCreationStatus.Success, new MemoryLayer { Name = MapsuiBasemapFactory.BasemapLayerName }));
        }
    }

    private sealed class FakeResolver : IMapSourceResolver
    {
        public ValueTask<MapSourceResolutionResult> ResolveAsync(string sourceId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var capabilities = new MapSourceCapabilities(true, true, false, false, false);
            var definition = new MapSourceDefinition(sourceId, "test", sourceId, MapAccessKind.HttpXyz, MapArchiveFormat.None, MapTileContentFormat.RasterPng, "https://example.test/{z}/{x}/{y}", 0, 18, "test", [], MapCredentialRequirement.None, capabilities, true, false);
            var policy = new MapUsagePolicy("test", null, new DateOnly(2026, 1, 1), "Test", true, true, false, false, false, false);
            var resolved = new ResolvedMapSource(sourceId, MapSourceOrigin.Catalog, new("test", "Test"), new("test", "test", "Test"), definition, policy, [], new(MapCredentialRequirement.None, true), definition.UriTemplate);
            return ValueTask.FromResult(new MapSourceResolutionResult(MapSourceResolutionStatus.None, resolved));
        }
    }
}
