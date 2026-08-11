using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Http;
using MissionPlanner.Maps.Policy;
using MissionPlanner.Maps.Sources;
using NSubstitute;

namespace MissionPlanner.Core.Tests.Maps;

/// <summary>Verifies the live policy, validator, authentication, and cache HTTP path.</summary>
public sealed class MapHttpResourceFetcherTests
{
    /// <summary>Verifies a fresh response is served from cache without another network request.</summary>
    [Fact]
    public async Task FreshEntryReturnsFromCache()
    {
        using var fixture = new Fixture(_ => Response(HttpStatusCode.OK, [1, 2, 3], TimeSpan.FromMinutes(5)));
        var first = await fixture.FetchAsync();
        var second = await fixture.FetchAsync();
        first.FromCache.Should().BeFalse();
        second.FromCache.Should().BeTrue();
        fixture.RequestCount.Should().Be(1);
    }

    /// <summary>Verifies a stale ETag is revalidated and a 304 reuses cached bytes.</summary>
    [Fact]
    public async Task StaleEtagRevalidatesWith304()
    {
        using var fixture = new Fixture(request =>
        {
            if (request.Headers.IfNoneMatch.Any()) return Response(HttpStatusCode.NotModified, []);
            var response = Response(HttpStatusCode.OK, [4, 5], TimeSpan.Zero);
            response.Headers.ETag = new EntityTagHeaderValue("\"v1\"");
            return response;
        });
        await fixture.FetchAsync();
        var second = await fixture.FetchAsync();
        second.Content.Should().Equal(4, 5);
        second.FromCache.Should().BeTrue();
        fixture.RequestCount.Should().Be(2);
    }

    /// <summary>Verifies no-store and disabled cache never persist provider bytes.</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task CacheCanBeDisabledOrNoStore(bool enabled, bool noStore)
    {
        using var fixture = new Fixture(_ =>
        {
            var response = Response(HttpStatusCode.OK, [7], TimeSpan.FromMinutes(5));
            response.Headers.CacheControl!.NoStore = noStore;
            return response;
        }, cacheEnabled: enabled);
        await fixture.FetchAsync();
        await fixture.FetchAsync();
        fixture.RequestCount.Should().Be(2);
    }

    /// <summary>Verifies provider authorization and rate-limit responses remain typed.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, MapHttpFetchStatus.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, MapHttpFetchStatus.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests, MapHttpFetchStatus.RateLimited)]
    public async Task HttpFailureIsTyped(HttpStatusCode statusCode, MapHttpFetchStatus expected)
    {
        using var fixture = new Fixture(_ => Response(statusCode, []));
        (await fixture.FetchAsync()).Status.Should().Be(expected);
    }

    /// <summary>Verifies concurrent identical tile requests are coalesced.</summary>
    [Fact]
    public async Task ConcurrentSameKeyUsesOneRequest()
    {
        using var fixture = new Fixture(_ =>
        {
            Thread.Sleep(50);
            return Response(HttpStatusCode.OK, [9], TimeSpan.FromMinutes(1));
        });
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => fixture.FetchAsync().AsTask()));
        fixture.RequestCount.Should().Be(1);
    }

    /// <summary>Verifies typed provider metadata injects an API key without ID-prefix parsing.</summary>
    [Fact]
    public async Task TypedQueryAuthenticationIsInjected()
    {
        Uri? observed = null;
        using var fixture = new Fixture(request =>
        {
            observed = request.RequestUri;
            return Response(HttpStatusCode.OK, [1]);
        }, authenticated: true);
        await fixture.FetchAsync();
        observed!.Query.Should().Contain("key=secret");
    }

    private static HttpResponseMessage Response(HttpStatusCode status, byte[] content, TimeSpan? maxAge = null)
    {
        var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(content) };
        response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = maxAge };
        return response;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), $"map-fetch-{Guid.NewGuid():N}");
        private readonly CountingHandler handler;
        private readonly MapHttpResourceFetcher fetcher;
        private readonly ResolvedMapSource source;

        public Fixture(Func<HttpRequestMessage, HttpResponseMessage> response, bool cacheEnabled = true, bool authenticated = false)
        {
            handler = new(response);
            var secrets = Substitute.For<IMapSecretStore>();
            secrets.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("secret");
            fetcher = new(new MapHttpClientFactory(handler, new("MissionPlanner-Test/1.0", TimeSpan.FromSeconds(2))), new(root, 1_048_576), new MapPolicyEvaluator(), secrets, new RuntimeSettings(cacheEnabled));
            var catalog = BuiltInMapCatalog.Load();
            var definition = catalog.Sources.Single(item => item.Id == "osm-standard");
            if (authenticated)
                definition = definition with { Id = "typed-auth", CredentialRequirement = MapCredentialRequirement.ApiKey, AuthenticationStrategy = MapAuthenticationStrategy.QueryApiKey, AuthenticationName = "key" };
            var product = catalog.Products.Single(item => item.Id == definition.ProductId);
            source = new(definition.Id, MapSourceOrigin.Catalog, catalog.Providers.Single(item => item.Id == product.ProviderId), product, definition, catalog.Policies.Single(item => item.Id == definition.PolicyId), [], new(definition.CredentialRequirement, true), definition.UriTemplate);
        }

        public int RequestCount => handler.RequestCount;

        public ValueTask<MapHttpFetchResult> FetchAsync() => fetcher.FetchAsync(new(source, new Uri("https://tiles.test/0/0/0.png"), MapHttpResourceKind.RasterTile, "0/0/0"), TestContext.Current.CancellationToken);

        public void Dispose()
        {
            handler.Dispose();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed record RuntimeSettings(bool CacheEnabled) : IMapHttpRuntimeSettings
    {
        public long CacheLimitBytes => 1_048_576;
    }

    private sealed class CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int RequestCount;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref RequestCount);
            return Task.FromResult(response(request));
        }
    }
}
