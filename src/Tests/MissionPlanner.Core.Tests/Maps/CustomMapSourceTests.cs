using System.Net;
using FluentAssertions;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Custom;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Http;

namespace MissionPlanner.Core.Tests.Maps;

public sealed class CustomMapSourceTests
{
    [Fact]
    public void Validator_AcceptsXyzWarnsOnHttpAndRejectsSecrets()
    {
        var valid = Source("https://tiles.local/{z}/{x}/{y}.png");
        CustomMapSourceValidator.Validate(valid).Should().BeEmpty();
        CustomMapSourceValidator.Validate(valid with { Endpoint = "http://tiles.local/{z}/{x}/{y}.png" }).Should().ContainSingle(issue => issue.IsWarning);
        CustomMapSourceValidator.Validate(valid with { Endpoint = "https://tiles.local/{z}/{x}/{y}.png?api_key=secret" }).Should().Contain(issue => !issue.IsWarning && issue.Path == "endpoint");
        CustomMapSourceValidator.Validate(valid with { Endpoint = "https://tiles.local/{z}/{x}.png" }).Should().Contain(issue => issue.Message.Contains("{y}", StringComparison.Ordinal));
    }

    [Fact]
    public void MetadataParser_ReadsWmsAndWmtsIdentifiers()
    {
        const string xml = "<Capabilities xmlns='urn:test'><ServiceIdentification><Title>Local maps</Title></ServiceIdentification><Contents><Layer><Identifier>base</Identifier></Layer><TileMatrixSet><Identifier>WebMercator</Identifier></TileMatrixSet></Contents></Capabilities>";
        var metadata = MapServiceMetadataParser.Parse(xml);
        metadata.ServiceTitle.Should().Be("Local maps");
        metadata.LayerNames.Should().Contain("base");
        metadata.TileMatrixSets.Should().Contain("WebMercator");
    }

    [Fact]
    public async Task Store_RoundTripsWithoutSecretMaterialAndDeleteFallsBack()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mp-custom-map-{Guid.NewGuid():N}");
        var file = Path.Combine(root, "sources.json");
        try
        {
            var store = new JsonCustomMapSourceStore(file);
            var source = Source("https://tiles.local/{z}/{x}/{y}.png") with { CredentialRequirement = MapCredentialRequirement.ApiKey };
            var service = new CustomMapSourceService(store, new MapHttpClientFactory(new StubHandler(), MapHttpOptions.Default));
            await service.SaveAsync(source, TestContext.Current.CancellationToken);
            (await store.LoadAsync(TestContext.Current.CancellationToken)).Should().ContainSingle().Which.Should().Be(source);
            (await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken)).ToLowerInvariant().Should().NotContain("secret");
            (await service.DeleteAsync(source.Id, source.Id, TestContext.Current.CancellationToken)).Should().Be("osm-standard");
            (await store.LoadAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task TestConnection_ParsesCapabilitiesAndReportsMissingLayer()
    {
        const string xml = "<WMS_Capabilities><Service><Title>Self hosted</Title></Service><Capability><Layer><Name>other</Name></Layer></Capability></WMS_Capabilities>";
        var service = new CustomMapSourceService(new MemoryStore(), new MapHttpClientFactory(new StubHandler(xml), MapHttpOptions.Default));
        var source = Source("https://maps.local/wms") with { AccessKind = MapAccessKind.Wms, LayerName = "base" };
        var status = await service.TestAsync(source, TestContext.Current.CancellationToken);
        status.Succeeded.Should().BeFalse();
        status.Message.Should().Contain("not advertised");
    }

    private static CustomMapSourceSettings Source(string endpoint) => new("custom", "Custom", MapAccessKind.HttpXyz, endpoint, 0, 18, null, null, null, MapCredentialRequirement.None, "© Local operator", true);

    private sealed class StubHandler(string content = "ok") : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) });
    }

    private sealed class MemoryStore : ICustomMapSourceStore
    {
        public ValueTask<IReadOnlyList<CustomMapSourceSettings>> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<CustomMapSourceSettings>>([]);
        public ValueTask SaveAsync(IReadOnlyList<CustomMapSourceSettings> sources, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
