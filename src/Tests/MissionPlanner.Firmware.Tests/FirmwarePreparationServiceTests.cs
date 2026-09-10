using FluentAssertions;
using MissionPlanner.Firmware.Downloads;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Preparation;
using MissionPlanner.Firmware.Configuration;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwarePreparationServiceTests
{
    [Fact]
    public async Task LocalImportValidatesAndCachesContentWithoutClaimingCompatibility()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new MissionPlanner.Firmware.Configuration.FirmwareOptions());
        var store = new FileSystemFirmwareArtifactStore(new TestPaths(), options, TimeProvider.System);
        var reader = new MissionPlanner.Firmware.Images.ApjFirmwarePackageReader(options);
        var service = new FirmwarePreparationService(new FakeDownloader(Download(50, false)), reader, store, options);
        await using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "valid.apj"));
        var first = await service.ImportAsync(stream, "local.apj", cancellationToken: TestContext.Current.CancellationToken);
        stream.Position = 0;
        var second = await service.ImportAsync(stream, "renamed.apj", cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(first.WasCacheHit);
        Assert.True(second.WasCacheHit);
        Assert.Equal(first.ArtifactMetadata.CacheKey, second.ArtifactMetadata.CacheKey);
        Assert.Equal(64, first.ArtifactMetadata.Sha256.Length);
        Assert.Equal(50, first.Package.BoardId);
        var local = FirmwareArtifactSummary.FromLocal(first);
        var online = FirmwareArtifactSummary.FromOnline(new(Entry(50), first.ArtifactMetadata,
            first.Package, first.ArtifactMetadata.Sha256, false, first.ArtifactMetadata.CacheKey, []));
        Assert.Equal(local.BoardId, online.BoardId);
        Assert.Equal(local.ImageSize, online.ImageSize);
        Assert.True(local.ArtifactValid);
        Assert.False(local.TargetCompatible);
        await using var invalid = new MemoryStream("{truncated"u8.ToArray());
        await Assert.ThrowsAsync<FirmwarePackageException>(() => service.ImportAsync(invalid, "broken.apj", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Single(await store.EnumerateAsync(TestContext.Current.CancellationToken));
    }

    private sealed class TestPaths : IFirmwareCachePathProvider
    {
        public string CacheRoot { get; } = Path.Combine(Path.GetTempPath(), "MissionPlannerLocalImportTests", Guid.NewGuid().ToString("N"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturnsFreshOrCachedValidatedPackage(bool cacheHit)
    {
        var entry = Entry(50);
        var service = new FirmwarePreparationService(new FakeDownloader(Download(50, cacheHit)));
        var result = await service.PrepareAsync(new(entry), cancellationToken: TestContext.Current.CancellationToken);
        result.PackageBoardId.Should().Be(50);
        result.WasCacheHit.Should().Be(cacheHit);
        result.Sha256.Should().HaveLength(64);
    }

    [Fact]
    public async Task RejectsManifestPackageBoardMismatch()
    {
        var service = new FirmwarePreparationService(new FakeDownloader(Download(51, false)));
        var act = () => service.PrepareAsync(new(Entry(50)), cancellationToken: TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<FirmwarePackageException>().WithMessage("*does not match*");
    }

    [Fact]
    public async Task PropagatesCancellationWithoutHardwareDependencies()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new FirmwarePreparationService(new CancellingDownloader());
        var act = () => service.PrepareAsync(new(Entry(50)), cancellationToken: cancellation.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static FirmwareManifestEntry Entry(int boardId) => new(new FirmwareVersion("1.0"), FirmwareReleaseChannel.Stable,
        new FirmwareBoardTarget(boardId, "Test", FirmwareVehicleType.Copter),
        new FirmwareArtifact(new Uri("https://example.test/test.apj"), FirmwareImageFormat.Apj));
    private static DownloadedFirmwareArtifact Download(int boardId, bool cacheHit)
    {
        var metadata = new FirmwareArtifactMetadata("cache-key", new Uri("https://example.test/test.apj"), DateTimeOffset.UtcNow, 4, new string('A', 64));
        return new(new Stored(metadata), new ApjFirmwarePackage(boardId, new byte[] { 1, 2, 3, 4 }, 16), metadata, cacheHit);
    }
    private sealed class FakeDownloader(DownloadedFirmwareArtifact result) : IFirmwareArtifactDownloader
    {
        public Task<DownloadedFirmwareArtifact> DownloadAsync(FirmwareArtifact artifact, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }
    private sealed class CancellingDownloader : IFirmwareArtifactDownloader
    {
        public Task<DownloadedFirmwareArtifact> DownloadAsync(FirmwareArtifact artifact, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default) => Task.FromCanceled<DownloadedFirmwareArtifact>(cancellationToken);
    }
    private sealed class Stored(FirmwareArtifactMetadata metadata) : IFirmwareStoredArtifact
    {
        public FirmwareArtifactMetadata Metadata => metadata;
        public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream());
    }
}
