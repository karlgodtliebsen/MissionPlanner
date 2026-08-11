using FluentAssertions;
using MissionPlanner.App.Maps;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Http;
using MissionPlanner.Maps.Policy;
using MissionPlanner.Maps.Sources;
using NSubstitute;

namespace MissionPlanner.Core.Tests.Maps;

/// <summary>Verifies typed routing through the production Mapsui composite.</summary>
public sealed class CompositeMapsuiBasemapFactoryTests
{
    /// <summary>Verifies every built-in raster and blank source creates the stable basemap slot.</summary>
    [Theory]
    [InlineData("osm-standard")]
    [InlineData("esri-world-topo")]
    [InlineData("esri-world-physical")]
    [InlineData("esri-world-shaded-relief")]
    [InlineData("esri-world-dark-gray")]
    [InlineData("no-map")]
    public async Task CreatesBuiltInSource(string sourceId)
    {
        var resolver = Resolver();
        var resolved = await resolver.ResolveAsync(sourceId, TestContext.Current.CancellationToken);

        var result = await Factory().CreateAsync(resolved.Source!, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Layer!.Name.Should().Be(MapsuiBasemapFactory.BasemapLayerName);
    }

    /// <summary>Verifies a credentialed hosted raster source reaches its live adapter.</summary>
    [Fact]
    public async Task CreatesHostedRasterSource()
    {
        var secretStore = Substitute.For<IMapSecretStore>();
        secretStore.GetAsync("maps.credentials.stadia-outdoors", Arg.Any<CancellationToken>()).Returns("secret");
        var resolved = await Resolver(secretStore).ResolveAsync("stadia-outdoors", TestContext.Current.CancellationToken);

        var result = await Factory(secretStore).CreateAsync(resolved.Source!, TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>Verifies custom XYZ is supported while unimplemented custom WMS remains typed unsupported.</summary>
    [Theory]
    [InlineData(MapAccessKind.HttpXyz, MapBasemapCreationStatus.Success)]
    [InlineData(MapAccessKind.Wms, MapBasemapCreationStatus.Unsupported)]
    public async Task RoutesCustomRasterByAccessKind(MapAccessKind accessKind, MapBasemapCreationStatus expected)
    {
        var source = ResolvedCustom(accessKind);
        var result = await Factory().CreateAsync(source, TestContext.Current.CancellationToken);
        result.Status.Should().Be(expected);
    }

    /// <summary>Verifies an installed raster MBTiles source reaches the read-only archive adapter.</summary>
    [Fact]
    public async Task CreatesOfflineMbTilesSource()
    {
        var archive = Path.GetTempFileName();
        try
        {
            var catalog = BuiltInMapCatalog.Load();
            var definition = catalog.Sources.Single(source => source.Id == "raster-mbtiles-template") with { Id = "pack:test:1", IsEnabledByDefault = true, IsFutureCandidate = false };
            var product = catalog.Products.Single(item => item.Id == definition.ProductId);
            var source = new ResolvedMapSource(definition.Id, MapSourceOrigin.InstalledPack, catalog.Providers.Single(item => item.Id == product.ProviderId), product, definition, catalog.Policies.Single(item => item.Id == definition.PolicyId), [], new(MapCredentialRequirement.None, true), archive);

            var result = await Factory().CreateAsync(source, TestContext.Current.CancellationToken);

            result.IsSuccess.Should().BeTrue();
        }
        finally
        {
            File.Delete(archive);
        }
    }

    private static CompositeMapsuiBasemapFactory Factory(IMapSecretStore? secrets = null)
    {
        secrets ??= Substitute.For<IMapSecretStore>();
        return new(new MapsuiMbTilesSourceFactory(), Substitute.For<IMapHttpResourceFetcher>());
    }

    private static MapSourceResolver Resolver(IMapSecretStore? secrets = null)
    {
        var packs = Substitute.For<MissionPlanner.Maps.Offline.IOfflineMapPackRepository>();
        var custom = Substitute.For<MissionPlanner.Maps.Custom.ICustomMapSourceStore>();
        custom.LoadAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<MissionPlanner.Maps.Custom.CustomMapSourceSettings>());
        return new(new BuiltInMapCatalogService(), new MapPolicyEvaluator(), secrets ?? Substitute.For<IMapSecretStore>(), packs, custom);
    }

    private static ResolvedMapSource ResolvedCustom(MapAccessKind accessKind)
    {
        var catalog = BuiltInMapCatalog.Load();
        var template = catalog.Sources.Single(source => source.Id == "custom-raster-template") with
        {
            Id = "custom:test",
            AccessKind = accessKind,
            UriTemplate = "https://example.test/{z}/{x}/{y}.png",
            IsEnabledByDefault = true,
            IsFutureCandidate = false
        };
        var product = catalog.Products.Single(item => item.Id == template.ProductId);
        return new(template.Id, MapSourceOrigin.Custom,
            catalog.Providers.Single(item => item.Id == product.ProviderId), product, template,
            catalog.Policies.Single(item => item.Id == template.PolicyId), [],
            new(MapCredentialRequirement.None, true), template.UriTemplate);
    }
}
