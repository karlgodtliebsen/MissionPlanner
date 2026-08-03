using System.Net;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Downloads;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Images;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwareArtifactDownloaderTests
{
    [Fact]
    public async Task DownloadsHashesParsesAndAtomicallyCommits()
    {
        var bytes = ValidPackage();
        var store = new MemoryStore();
        var downloader = CreateDownloader(new StaticHandler(bytes), store);

        var result = await downloader.DownloadAsync(Artifact(bytes), cancellationToken: TestContext.Current.CancellationToken);

        result.Package.BoardId.Should().Be(50);
        result.Metadata.Size.Should().Be(bytes.Length);
        result.Metadata.Sha256.Should().Be(Convert.ToHexString(SHA256.HashData(bytes)));
        result.FromCache.Should().BeFalse();
        store.Commits.Should().Be(1);
    }

    [Fact]
    public async Task ValidCommittedArtifactIsReusedWithoutHttp()
    {
        var bytes = ValidPackage();
        var store = new MemoryStore();
        var handler = new StaticHandler(bytes);
        var downloader = CreateDownloader(handler, store);
        _ = await downloader.DownloadAsync(Artifact(bytes), cancellationToken: TestContext.Current.CancellationToken);

        var cached = await downloader.DownloadAsync(Artifact(bytes), cancellationToken: TestContext.Current.CancellationToken);

        cached.FromCache.Should().BeTrue();
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task OversizedDownloadStopsAndDeletesPartialArtifact()
    {
        var bytes = ValidPackage();
        var store = new MemoryStore();
        var downloader = CreateDownloader(new StaticHandler(bytes), store, maximumBytes: bytes.Length - 1);

        var act = async () => await downloader.DownloadAsync(Artifact(bytes), cancellationToken: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FirmwareDownloadException>();
        store.PartialsDisposed.Should().Be(1);
        store.Commits.Should().Be(0);
    }

    [Fact]
    public async Task CorruptPackageFailsBeforeCommit()
    {
        var bytes = "not an APJ package"u8.ToArray();
        var store = new MemoryStore();
        var downloader = CreateDownloader(new StaticHandler(bytes), store);

        var act = async () => await downloader.DownloadAsync(Artifact(bytes), cancellationToken: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FirmwareDownloadException>();
        store.Commits.Should().Be(0);
        store.PartialsDisposed.Should().Be(1);
    }

    [Fact]
    public async Task CancellationDeletesPartialArtifact()
    {
        var store = new MemoryStore();
        var downloader = CreateDownloader(new WaitingHandler(), store);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var bytes = ValidPackage();

        var task = downloader.DownloadAsync(Artifact(bytes), cancellationToken: cancellation.Token);
        cancellation.Cancel();
        var act = async () => await task;

        await act.Should().ThrowAsync<OperationCanceledException>();
        store.PartialsDisposed.Should().Be(1);
        store.Commits.Should().Be(0);
    }

    [Fact]
    public async Task ChecksumMismatchDeletesPartialArtifact()
    {
        var bytes = ValidPackage();
        var store = new MemoryStore();
        var downloader = CreateDownloader(new StaticHandler(bytes), store);
        var artifact = new FirmwareArtifact(new Uri("https://firmware.example/test.apj"), FirmwareImageFormat.Apj, bytes.Length, new string('0', 64));

        var act = async () => await downloader.DownloadAsync(artifact, cancellationToken: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<FirmwareDownloadException>();
        store.Commits.Should().Be(0);
    }

    [Fact]
    public async Task MissingContentLengthReportsByteProgressWithoutPercentage()
    {
        var bytes = ValidPackage();
        var reports = new List<FirmwareProgress>();
        var downloader = CreateDownloader(new NoLengthHandler(bytes), new MemoryStore());

        _ = await downloader.DownloadAsync(Artifact(bytes), new InlineProgress(reports.Add), TestContext.Current.CancellationToken);

        reports.Should().NotBeEmpty();
        reports.Should().OnlyContain(report => report.Percentage == null && report.TotalBytes == null);
        reports[^1].CompletedBytes.Should().Be(bytes.Length);
    }

    private static FirmwareArtifactDownloader CreateDownloader(HttpMessageHandler handler, IFirmwareArtifactStore store, long maximumBytes = 1024 * 1024) =>
        new(new HttpClient(handler), store, new ApjFirmwarePackageReader(Options.Create(new FirmwareOptions())),
            Options.Create(new FirmwareOptions { MaximumArtifactBytes = maximumBytes }), TimeProvider.System);

    private static FirmwareArtifact Artifact(byte[] bytes) => new(
        new Uri("https://firmware.example/test.apj"), FirmwareImageFormat.Apj, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
    private static byte[] ValidPackage() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "valid.apj"));

    private sealed class StaticHandler(byte[] bytes) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        }
    }

    private sealed class WaitingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException();
        }
    }

    private sealed class NoLengthHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new NoLengthContent(bytes) });
        private sealed class NoLengthContent(byte[] bytes) : HttpContent
        {
            protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
            protected override bool TryComputeLength(out long length) { length = 0; return false; }
        }
    }

    private sealed class InlineProgress(Action<FirmwareProgress> report) : IProgress<FirmwareProgress>
    {
        public void Report(FirmwareProgress value) => report(value);
    }

    private sealed class MemoryStore : IFirmwareArtifactStore
    {
        private Stored? stored;
        public int PartialsDisposed { get; private set; }
        public int Commits { get; private set; }
        public Task<IFirmwareStoredArtifact?> TryGetAsync(string cacheKey, CancellationToken cancellationToken = default) => Task.FromResult<IFirmwareStoredArtifact?>(stored?.Metadata.CacheKey == cacheKey ? stored : null);
        public Task<IFirmwareArtifactWriter> CreateTemporaryAsync(string cacheKey, CancellationToken cancellationToken = default) => Task.FromResult<IFirmwareArtifactWriter>(new Writer(this));

        private sealed class Writer(MemoryStore owner) : IFirmwareArtifactWriter
        {
            private bool committed;
            public Stream Stream { get; } = new MemoryStream();
            public Task<IFirmwareStoredArtifact> CommitAsync(FirmwareArtifactMetadata metadata, CancellationToken cancellationToken = default)
            {
                committed = true;
                owner.Commits++;
                owner.stored = new Stored(((MemoryStream)Stream).ToArray(), metadata);
                return Task.FromResult<IFirmwareStoredArtifact>(owner.stored);
            }
            public ValueTask DisposeAsync() { if (!committed) owner.PartialsDisposed++; Stream.Dispose(); return ValueTask.CompletedTask; }
        }
        private sealed class Stored(byte[] bytes, FirmwareArtifactMetadata metadata) : IFirmwareStoredArtifact
        {
            public FirmwareArtifactMetadata Metadata => metadata;
            public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream(bytes, false));
        }
    }
}
