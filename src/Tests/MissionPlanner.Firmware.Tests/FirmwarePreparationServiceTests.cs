using FluentAssertions;
using MissionPlanner.Firmware.Downloads;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Preparation;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwarePreparationServiceTests
{
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
