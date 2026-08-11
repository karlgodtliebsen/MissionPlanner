using System.Net;
using FluentAssertions;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Esri;
using MissionPlanner.Maps.Http;
using MissionPlanner.Maps.Policy;
using MissionPlanner.Maps.Sources;
using MissionPlanner.Maps.Credentials;
using NSubstitute;

namespace MissionPlanner.Core.Tests.Maps;

public sealed class EsriMapSourceTests
{
    [Theory]
    [InlineData("esri-world-topo")]
    [InlineData("esri-world-physical")]
    [InlineData("esri-world-shaded-relief")]
    [InlineData("esri-world-dark-gray")]
    public void CurrentStyle_AllowsInteractiveCacheAndDeniesPackHarvesting(string sourceId)
    {
        var catalog = BuiltInMapCatalog.Load();
        var source = catalog.Sources.Single(item => item.Id == sourceId);
        var policy = catalog.Policies.Single(item => item.Id == source.PolicyId);
        var evaluator = new MapPolicyEvaluator();
        evaluator.Evaluate(source, policy, MapOperation.InteractiveUse).IsAllowed.Should().BeTrue();
        evaluator.Evaluate(source, policy, MapOperation.ClientDiskCache).IsAllowed.Should().BeTrue();
        evaluator.Evaluate(source, policy, MapOperation.OfflineAreaDownload).IsAllowed.Should().BeFalse();
        evaluator.Evaluate(source, policy, MapOperation.BulkPrefetch).IsAllowed.Should().BeFalse();
        evaluator.Evaluate(source, policy, MapOperation.RedistributedPack).IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task AttributionResolver_MergesServiceCopyrightWithFallback()
    {
        var resolver = Resolver("{\"copyrightText\":\"Esri, HERE, Garmin\"}", HttpStatusCode.OK);
        var entries = await resolver.ResolveAsync("esri-world-topo", TestContext.Current.CancellationToken);
        entries.Should().Contain(item => item.Id == "esri");
        entries.Should().Contain(item => item.Text == "Esri, HERE, Garmin");
    }

    [Fact]
    public async Task AttributionResolver_UsesFallbackWhenMetadataFails()
    {
        var entries = await Resolver("failure", HttpStatusCode.ServiceUnavailable).ResolveAsync("esri-world-topo", TestContext.Current.CancellationToken);
        entries.Should().ContainSingle().Which.Id.Should().Be("esri");
    }

    [Fact]
    public void AuthenticatedUri_RedactsToken()
    {
        var authenticated = EsriRequestUriBuilder.WithToken(new Uri("https://example.test/MapServer/tile/0/0/0"), "top-secret");
        authenticated.ToString().Should().Contain("top-secret");
        EsriRequestUriBuilder.ToDiagnosticString(authenticated).Should().NotContain("top-secret");
    }

    private static EsriAttributionResolver Resolver(string content, HttpStatusCode status)
    {
        var catalog = BuiltInMapCatalog.Load();
        var definition = catalog.Sources.Single(item => item.Id == "esri-world-topo");
        var product = catalog.Products.Single(item => item.Id == definition.ProductId);
        var resolved = new ResolvedMapSource(definition.Id, MapSourceOrigin.Catalog, catalog.Providers.Single(item => item.Id == product.ProviderId), product, definition, catalog.Policies.Single(item => item.Id == definition.PolicyId), [catalog.Attributions.Single(item => item.Id == "esri")], new(MapCredentialRequirement.None, true), definition.UriTemplate);
        var sourceResolver = Substitute.For<IMapSourceResolver>();
        sourceResolver.ResolveAsync(definition.Id, Arg.Any<CancellationToken>()).Returns(new MapSourceResolutionResult(MapSourceResolutionStatus.None, resolved));
        var fetcher = Substitute.For<IMapHttpResourceFetcher>();
        fetcher.FetchAsync(Arg.Any<MapHttpFetchRequest>(), Arg.Any<CancellationToken>()).Returns(
            status == HttpStatusCode.OK
                ? new MapHttpFetchResult(MapHttpFetchStatus.Success, System.Text.Encoding.UTF8.GetBytes(content), false)
                : new MapHttpFetchResult(MapHttpFetchStatus.NetworkFailure, null, false));
        return new(catalog, sourceResolver, fetcher);
    }
}
