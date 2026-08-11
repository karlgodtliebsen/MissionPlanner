using System.Net;
using FluentAssertions;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Esri;
using MissionPlanner.Maps.Http;
using MissionPlanner.Maps.Policy;

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

    private static EsriAttributionResolver Resolver(string content, HttpStatusCode status) => new(
        BuiltInMapCatalog.Load(), new MapHttpClientFactory(new StubHandler(content, status), MapHttpOptions.Default));

    private sealed class StubHandler(string content, HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(content) });
    }
}
