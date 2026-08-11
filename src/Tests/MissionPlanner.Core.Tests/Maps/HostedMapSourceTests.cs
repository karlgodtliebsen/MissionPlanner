using System.Net;
using FluentAssertions;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Hosted;
using MissionPlanner.Maps.Policy;

namespace MissionPlanner.Core.Tests.Maps;

public sealed class HostedMapSourceTests
{
    [Theory]
    [InlineData("stadia-outdoors")]
    [InlineData("thunderforest-outdoors")]
    [InlineData("maptiler-streets")]
    public async Task HostedSource_IsDisabledUntilCredentialConfigured(string sourceId)
    {
        var store = new MemorySecretStore();
        var service = new HostedMapSourceService(BuiltInMapCatalog.Load(), store, new MapPolicyEvaluator());
        (await service.GetStateAsync(sourceId, TestContext.Current.CancellationToken)).IsSelectable.Should().BeFalse();
        await store.SetAsync($"maps.credentials.{sourceId}", "top-secret", TestContext.Current.CancellationToken);
        var state = await service.GetStateAsync(sourceId, TestContext.Current.CancellationToken);
        state.IsSelectable.Should().BeTrue();
        state.Attributions.Should().NotBeEmpty();
        state.PolicySummary.Should().Contain("offline area: denied").And.Contain("proxy/redistribution: denied");
    }

    [Theory]
    [InlineData("stadia-outdoors", "Authorization", "Stadia-Auth")]
    [InlineData("thunderforest-outdoors", "apikey", "top-secret")]
    [InlineData("maptiler-streets", "key", "top-secret")]
    public async Task AuthorizedRequest_InjectsCredentialOnlyAtRequestBoundary(string sourceId, string expectedName, string expectedValue)
    {
        var store = new MemorySecretStore();
        await store.SetAsync($"maps.credentials.{sourceId}", "top-secret", TestContext.Current.CancellationToken);
        var service = new HostedMapSourceService(BuiltInMapCatalog.Load(), store, new MapPolicyEvaluator());
        using var request = await service.CreateRequestAsync(sourceId, 1, 2, 3, TestContext.Current.CancellationToken);
        var serialized = request.ToString();
        serialized.Should().Contain(expectedName).And.Contain(expectedValue);
        MapDiagnosticRedactor.Redact(serialized, "top-secret").Should().NotContain("top-secret");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, HostedMapFailureKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, HostedMapFailureKind.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests, HostedMapFailureKind.RateLimited)]
    [InlineData(HttpStatusCode.BadGateway, HostedMapFailureKind.Network)]
    public void FailureClassifier_DistinguishesProviderFailures(HttpStatusCode status, HostedMapFailureKind expected)
    {
        HostedMapSourceService.ClassifyFailure(new HttpRequestException("sensitive transport detail"), status).Kind.Should().Be(expected);
        HostedMapSourceService.ClassifyFailure(new HttpRequestException("sensitive transport detail"), status).Message.Should().NotContain("sensitive");
    }

    private sealed class MemorySecretStore : IMapSecretStore
    {
        private readonly Dictionary<string, string> values = [];
        public ValueTask<string?> GetAsync(string key, CancellationToken cancellationToken = default) => ValueTask.FromResult(values.GetValueOrDefault(key));
        public ValueTask SetAsync(string key, string value, CancellationToken cancellationToken = default) { values[key] = value; return ValueTask.CompletedTask; }
        public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) { values.Remove(key); return ValueTask.CompletedTask; }
    }
}
