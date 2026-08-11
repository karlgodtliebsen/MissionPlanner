using System.Net;
using FluentAssertions;
using MissionPlanner.Maps.Attribution;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Http;
using MissionPlanner.Maps.Policy;
using MissionPlanner.Maps.Terrain;

namespace MissionPlanner.Core.Tests.Maps;

public sealed class MapPolicyAndInfrastructureTests
{
    [Fact]
    public async Task SrtmReader_InterpolatesBigEndianTerrainGrid()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mp-srtm-{Guid.NewGuid():N}.hgt");
        try
        {
            short[] samples = [100, 110, 120, 50, 60, 70, 0, 10, 20];
            var bytes = samples.SelectMany(value => new[] { (byte)(value >> 8), (byte)value }).ToArray();
            await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);

            (await SrtmHgtReader.ReadAsync(path, 55.5, 12.5, 55, 12, TestContext.Current.CancellationToken)).Should().Be(60);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void PolicyEvaluator_IntersectsCapabilityAndPolicyAndRestrictsOsm()
    {
        var catalog = BuiltInMapCatalog.Load();
        var source = catalog.Sources.Single(item => item.Id == "osm-standard");
        var policy = catalog.Policies.Single(item => item.Id == source.PolicyId);
        var evaluator = new MapPolicyEvaluator();
        evaluator.Evaluate(source, policy, MapOperation.InteractiveUse).IsAllowed.Should().BeTrue();
        evaluator.Evaluate(source, policy, MapOperation.ClientDiskCache).IsAllowed.Should().BeTrue();
        evaluator.Evaluate(source, policy, MapOperation.OfflineAreaDownload).IsAllowed.Should().BeFalse();
        evaluator.Evaluate(source, policy, MapOperation.BulkPrefetch).IsAllowed.Should().BeFalse();
        evaluator.Evaluate(source, policy, MapOperation.Proxy).IsAllowed.Should().BeFalse();
        evaluator.Evaluate(source, policy, MapOperation.RedistributedPack).IsAllowed.Should().BeFalse();
    }

    [Fact]
    public void PolicyEvaluator_UsesIndependentCapabilityAndPolicyFlags()
    {
        var capabilities = new MapSourceCapabilities(true, true, true, true, true, SupportsBulkPrefetch: true, SupportsProxy: true, SupportsRedistribution: true);
        var source = new MapSourceDefinition("source", "product", "Source", MapAccessKind.HttpXyz, MapArchiveFormat.None, MapTileContentFormat.RasterPng, "https://example.test/{z}/{x}/{y}", 0, 18, "policy", [], MapCredentialRequirement.None, capabilities, true, false);
        var policy = new MapUsagePolicy("policy", null, new DateOnly(2026, 8, 11), "Test", true, true, true, true, true, true, AllowBulkPrefetch: false, AllowProxy: true, AllowRedistribution: false);
        var evaluator = new MapPolicyEvaluator();

        evaluator.Evaluate(source, policy, MapOperation.OfflineAreaDownload).IsAllowed.Should().BeTrue();
        evaluator.Evaluate(source, policy, MapOperation.BulkPrefetch).IsAllowed.Should().BeFalse();
        evaluator.Evaluate(source, policy, MapOperation.Proxy).IsAllowed.Should().BeTrue();
        evaluator.Evaluate(source, policy, MapOperation.RedistributedPack).IsAllowed.Should().BeFalse();
        evaluator.Evaluate(source, policy, MapOperation.StaticExport).IsAllowed.Should().BeTrue();
        evaluator.Evaluate(source, policy, MapOperation.Printing).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task AttributionService_UsesVisibleContributorsAndDeduplicates()
    {
        var entry = new MapAttributionEntry("osm", "© OpenStreetMap contributors", null, true, true);
        IMapAttributionContributor[] contributors =
        [
            new Contributor("one", true, [entry]), new Contributor("two", true, [entry]),
            new Contributor("hidden", false, [new("hidden", "Hidden", null, true, true)])
        ];
        var snapshot = await new MapAttributionService().GetCurrentAsync(contributors, cancellationToken: TestContext.Current.CancellationToken);
        snapshot.Entries.Should().ContainSingle().Which.Id.Should().Be("osm");
        snapshot.CompactText.Should().Be("© OpenStreetMap contributors");
    }

    [Fact]
    public void DiagnosticRedactor_RemovesKnownAndQuerySecrets()
    {
        MapDiagnosticRedactor.Redact("https://maps.test/tile?api_key=secret&x=1 token-secret", "token-secret")
            .Should().Be("https://maps.test/tile?api_key=[REDACTED]&x=1 [REDACTED]");
    }

    [Fact]
    public async Task DiskCache_IsolatesNamespacesAndSupportsClear()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mp-map-cache-{Guid.NewGuid():N}");
        try
        {
            var cache = new MapHttpDiskCache(root, 1024);
            var first = new MapCacheNamespace("first", "product", "style");
            var second = new MapCacheNamespace("second", "product", "style");
            var metadata = new MapHttpCacheMetadata(DateTimeOffset.UtcNow.AddMinutes(1), "\"tag\"", DateTimeOffset.UtcNow);
            await cache.WriteAsync(first, "tile", [1, 2, 3], metadata, TestContext.Current.CancellationToken);
            (await cache.ReadAsync(first, "tile", TestContext.Current.CancellationToken)).Should().NotBeNull();
            (await cache.ReadAsync(second, "tile", TestContext.Current.CancellationToken)).Should().BeNull();
            cache.Clear(first);
            (await cache.ReadAsync(first, "tile", TestContext.Current.CancellationToken)).Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void HttpFactory_ConfiguresUserAgentAndTimeout()
    {
        using var handler = new StubHandler();
        var options = new MapHttpOptions("MissionPlanner-Test/1.0", TimeSpan.FromSeconds(3));
        using var client = new MapHttpClientFactory(handler, options).CreateClient();
        client.Timeout.Should().Be(TimeSpan.FromSeconds(3));
        client.DefaultRequestHeaders.UserAgent.ToString().Should().Be("MissionPlanner-Test/1.0");
    }

    [Fact]
    public async Task HttpCancellation_IsObserved()
    {
        using var handler = new StubHandler();
        using var client = new MapHttpClientFactory(handler, MapHttpOptions.Default).CreateClient();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var action = () => client.GetAsync("https://example.test", cancellation.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private sealed record Contributor(string ContributorId, bool IsVisible, IReadOnlyCollection<MapAttributionEntry> Attributions) : IMapAttributionContributor;
    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
