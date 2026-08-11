using System.Net;
using FluentAssertions;
using MissionPlanner.Maps.Feed;
using MissionPlanner.Maps.Http;
using MissionPlanner.Maps.Offline;
using NSubstitute;

namespace MissionPlanner.Core.Tests.Maps;

/// <summary>Verifies reviewed pack-feed validation and transactional updates.</summary>
public sealed class MapPackFeedTests
{
    /// <summary>Verifies a signed HTTPS feed is accepted.</summary>
    [Fact]
    public async Task FeedAcceptsVerifiedHttpsDocument()
    {
        var payload = Payload(Entry(4));
        var document = Serialize(new SignedMapPackFeed(payload, Convert.ToBase64String([1])));
        var client = new MapPackFeedClient(Factory(document), new TestVerifier(true));

        var result = await client.GetAsync(new Uri("https://packs.example/feed.json"), TestContext.Current.CancellationToken);

        result.Entries.Should().ContainSingle();
    }

    /// <summary>Verifies malformed or untrusted feeds fail closed.</summary>
    [Theory]
    [InlineData(false, "https://packs.example/feed.json")]
    [InlineData(true, "http://packs.example/feed.json")]
    public async Task FeedRejectsInvalidSignatureOrTransport(bool validSignature, string uri)
    {
        var payload = Payload(Entry(4));
        var document = Serialize(new SignedMapPackFeed(payload, Convert.ToBase64String([1])));
        var client = new MapPackFeedClient(Factory(document), new TestVerifier(validSignature));

        var action = () => client.GetAsync(new Uri(uri), TestContext.Current.CancellationToken).AsTask();

        await action.Should().ThrowAsync<InvalidDataException>();
    }

    /// <summary>Verifies partial artifact downloads never reach the installer.</summary>
    [Fact]
    public async Task InstallerRejectsPartialDownload()
    {
        var installer = Substitute.For<IOfflineMapPackInstaller>();
        var service = new MapPackFeedInstaller(Factory([1, 2]), installer, EmptyRepository());

        var action = () => service.InstallAsync(Entry(4), new Version(2, 0), new Version(5, 0), cancellationToken: TestContext.Current.CancellationToken).AsTask();

        await action.Should().ThrowAsync<InvalidDataException>();
        await installer.DidNotReceiveWithAnyArgs().InstallAsync(default!, default!, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies incompatible renderer versions are rejected before download.</summary>
    [Fact]
    public async Task InstallerRejectsIncompatibleRenderer()
    {
        var service = new MapPackFeedInstaller(Factory([1, 2, 3, 4]), Substitute.For<IOfflineMapPackInstaller>(), EmptyRepository());

        var action = () => service.InstallAsync(Entry(4) with { MinimumRendererVersion = "9.0" }, new Version(2, 0), new Version(5, 0)).AsTask();

        await action.Should().ThrowAsync<NotSupportedException>();
    }

    /// <summary>Verifies downgrade attempts are rejected.</summary>
    [Fact]
    public async Task InstallerRejectsDowngrade()
    {
        var repository = Substitute.For<IOfflineMapPackRepository>();
        repository.ListAsync(Arg.Any<CancellationToken>()).Returns([Installed("2.0")]);
        var service = new MapPackFeedInstaller(Factory([1, 2, 3, 4]), Substitute.For<IOfflineMapPackInstaller>(), repository);

        var action = () => service.InstallAsync(Entry(4) with { Manifest = Manifest(4) with { Version = "1.0" } }, new Version(2, 0), new Version(5, 0)).AsTask();

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    /// <summary>Verifies old versions are removed only after a successful atomic install.</summary>
    [Fact]
    public async Task UpgradeRemovesOldVersionAfterSuccess()
    {
        var repository = Substitute.For<IOfflineMapPackRepository>();
        repository.ListAsync(Arg.Any<CancellationToken>()).Returns([Installed("1.0")]);
        var installer = Substitute.For<IOfflineMapPackInstaller>();
        installer.InstallAsync(Arg.Any<OfflineMapPackManifest>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(Installed("2.0"));
        var service = new MapPackFeedInstaller(Factory([1, 2, 3, 4]), installer, repository);

        await service.InstallAsync(Entry(4), new Version(2, 0), new Version(5, 0), cancellationToken: TestContext.Current.CancellationToken);

        await repository.Received().RemoveAsync("test-pack", "1.0", null, Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies disk/install failure retains the previous working version.</summary>
    [Fact]
    public async Task FailedInstallRollsBackWithoutRemovingOldVersion()
    {
        var repository = Substitute.For<IOfflineMapPackRepository>();
        repository.ListAsync(Arg.Any<CancellationToken>()).Returns([Installed("1.0")]);
        var installer = Substitute.For<IOfflineMapPackInstaller>();
        installer.InstallAsync(Arg.Any<OfflineMapPackManifest>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns<ValueTask<InstalledOfflineMapPack>>(_ => throw new IOException("disk full"));
        var service = new MapPackFeedInstaller(Factory([1, 2, 3, 4]), installer, repository);

        var action = () => service.InstallAsync(Entry(4), new Version(2, 0), new Version(5, 0)).AsTask();

        await action.Should().ThrowAsync<IOException>();
        await repository.DidNotReceiveWithAnyArgs().RemoveAsync(default!, default!, default, TestContext.Current.CancellationToken);
    }

    private static MapPackFeedPayload Payload(params MapPackFeedEntry[] entries) => new(1, "1", DateTimeOffset.UnixEpoch, entries);

    private static MapPackFeedEntry Entry(long size) => new(Manifest(size), "reviewed-source", "reviewed-product", new Uri("https://packs.example/test.mbtiles"), "2.0", "5.0", [new Uri("https://packs.example/notices/test")]);

    private static OfflineMapPackManifest Manifest(long size) => new("test-pack", "2.0", "Test pack", "test.mbtiles", new string('0', 64), size, new(-10, -10, 10, 10), 1, 10, "EPSG:3857", "png", "Test attribution", "Test license");

    private static InstalledOfflineMapPack Installed(string version) => new(Manifest(4) with { Version = version }, "packs/test/" + version, "packs/test/" + version + "/test.mbtiles");

    private static IOfflineMapPackRepository EmptyRepository()
    {
        var repository = Substitute.For<IOfflineMapPackRepository>();
        repository.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        return repository;
    }

    private static IMapHttpClientFactory Factory(byte[] content) => new TestHttpFactory(content);

    private static byte[] Serialize(SignedMapPackFeed feed) =>
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(feed, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

    private sealed class TestVerifier(bool result) : IMapPackFeedSignatureVerifier
    {
        public bool Verify(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature) => result;
    }

    private sealed class TestHttpFactory(byte[] content) : IMapHttpClientFactory
    {
        public HttpClient CreateClient() => new(new Handler(content)) { Timeout = TimeSpan.FromSeconds(2) };

        private sealed class Handler(byte[] content) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = new ByteArrayContent(content);
                body.Headers.ContentLength = content.Length;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = body });
            }
        }
    }
}
