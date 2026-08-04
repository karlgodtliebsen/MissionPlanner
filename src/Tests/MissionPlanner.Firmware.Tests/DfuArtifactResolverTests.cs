using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Downloads;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Tests;

public sealed class DfuArtifactResolverTests
{
    [Theory]
    [InlineData(FirmwareVehicleType.Copter, "arducopter_with_bl.hex")]
    [InlineData(FirmwareVehicleType.Plane, "arduplane_with_bl.hex")]
    [InlineData(FirmwareVehicleType.Rover, "ardurover_with_bl.hex")]
    [InlineData(FirmwareVehicleType.Sub, "ardusub_with_bl.hex")]
    public async Task OfficialReleaseDerivesOnlyApprovedSiblingInSameDirectory(FirmwareVehicleType vehicle, string expectedName)
    {
        var downloader = new FakeDownloader();
        var resolver = CreateResolver(downloader);
        var request = OfficialRequest(vehicle, new Uri("https://firmware.ardupilot.org/Copter/stable/CubeOrange/firmware.apj"));

        await resolver.ResolveAsync(request, TestContext.Current.CancellationToken);

        downloader.Source.Should().Be(new Uri($"https://firmware.ardupilot.org/Copter/stable/CubeOrange/{expectedName}"));
        downloader.Platform.Should().Be("CubeOrange");
        downloader.BoardId.Should().Be(140);
    }

    [Fact]
    public async Task NonOfficialSourceIsRejectedBeforeDownloader()
    {
        var downloader = new FakeDownloader();
        var resolver = CreateResolver(downloader);
        var action = () => resolver.ResolveAsync(OfficialRequest(FirmwareVehicleType.Copter,
            new Uri("https://example.test/Copter/stable/Board/firmware.apj")), TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<DfuArtifactResolutionException>().WithMessage("*official*");
        downloader.Source.Should().BeNull();
    }

    [Fact]
    public async Task MissingOfficialSiblingRemainsAResolutionFailure()
    {
        var resolver = CreateResolver(new FakeDownloader { Failure = new DfuArtifactResolutionException("not found") });
        var action = () => resolver.ResolveAsync(OfficialRequest(FirmwareVehicleType.Copter,
            new Uri("https://firmware.ardupilot.org/Copter/stable/Board/firmware.apj")), TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<DfuArtifactResolutionException>().WithMessage("not found");
    }

    [Fact]
    public async Task LocalCustomHexIsInspectedAndWarnsWithoutCombinedFilenameClaim()
    {
        var path = Path.Combine(Path.GetTempPath(), $"custom-{Guid.NewGuid():N}.hex");
        await File.WriteAllTextAsync(path, ValidHexText(), TestContext.Current.CancellationToken);
        try
        {
            var request = new DfuInstallationRequest("CubeOrange", 140,
                new DfuDeviceDescriptor("usb1", 0x0483, 0xDF11, DfuDriverState.PresentReady), LocalHexPath: path);

            var artifact = await CreateResolver(new FakeDownloader()).ResolveAsync(request, TestContext.Current.CancellationToken);

            artifact.Metadata.DataBytes.Should().Be(2);
            artifact.Metadata.Warnings.Should().Contain(message => message.Contains("filename", StringComparison.OrdinalIgnoreCase));
            artifact.SourceUri.Should().BeNull();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task TrustedRedirectMirrorIsRecordedAndStoredWithHexExtension()
    {
        using var cache = new TemporaryCache();
        var options = new DfuOptions { OfficialFirmwareHosts = ["firmware.ardupilot.org", "mirror.ardupilot.org"] };
        var store = new FileSystemFirmwareArtifactStore(cache, Options.Create(new FirmwareOptions()), TimeProvider.System);
        var handler = new RedirectFixtureHandler(Encoding.ASCII.GetBytes(ValidHexText()),
            new Uri("https://mirror.ardupilot.org/Copter/stable/Board/arducopter_with_bl.hex"));
        var downloader = new DfuHexArtifactDownloader(new HttpClient(handler), store,
            new IntelHexInspector(Options.Create(options), TimeProvider.System), Options.Create(options), TimeProvider.System);

        var artifact = await downloader.DownloadAsync(
            new Uri("https://firmware.ardupilot.org/Copter/stable/Board/arducopter_with_bl.hex"), "Board", 140,
            TestContext.Current.CancellationToken);

        artifact.SourceUri!.Host.Should().Be("mirror.ardupilot.org");
        Path.GetExtension(artifact.LocalPath).Should().Be(".hex");
        File.Exists(artifact.LocalPath).Should().BeTrue();
    }

    private static DfuArtifactResolver CreateResolver(FakeDownloader downloader)
    {
        var options = Options.Create(new DfuOptions());
        return new DfuArtifactResolver(downloader, new IntelHexInspector(options, TimeProvider.System), options);
    }

    private static DfuInstallationRequest OfficialRequest(FirmwareVehicleType vehicle, Uri source)
    {
        var target = new FirmwareBoardTarget(140, "CubeOrange", vehicle);
        var entry = new FirmwareManifestEntry(new FirmwareVersion("4.6.0"), FirmwareReleaseChannel.Stable, target,
            new FirmwareArtifact(source, FirmwareImageFormat.Apj));
        return new DfuInstallationRequest("CubeOrange", 140,
            new DfuDeviceDescriptor("usb1", 0x0483, 0xDF11, DfuDriverState.PresentReady), ManifestEntry: entry);
    }

    private static string ValidHexText() => ":020000040800F2\n:020000000102FB\n:00000001FF\n";

    private sealed class FakeDownloader : IDfuHexArtifactDownloader
    {
        public Uri? Source { get; private set; }
        public string? Platform { get; private set; }
        public int? BoardId { get; private set; }
        public Exception? Failure { get; init; }
        public Task<DfuArtifact> DownloadAsync(Uri sourceUri, string platform, int? boardId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Source = sourceUri;
            Platform = platform;
            BoardId = boardId;
            if (Failure is not null) return Task.FromException<DfuArtifact>(Failure);
            return Task.FromResult(new DfuArtifact(Path.GetFileName(sourceUri.AbsolutePath), "artifact.hex",
                new DfuArtifactMetadata(1, 1, 0x08000000, 0x08000000, new string('A', 64), [new DfuMemoryRange(0x08000000, new byte[] { 1 })], []),
                sourceUri, platform, boardId));
        }
    }

    private sealed class RedirectFixtureHandler(byte[] bytes, Uri finalUri) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, finalUri),
                Content = new ByteArrayContent(bytes)
            });
        }
    }

    private sealed class TemporaryCache : IFirmwareCachePathProvider, IDisposable
    {
        public TemporaryCache()
        {
            CacheRoot = Path.Combine(Path.GetTempPath(), $"dfu-cache-{Guid.NewGuid():N}");
            Directory.CreateDirectory(CacheRoot);
        }
        public string CacheRoot { get; }
        public void Dispose() { if (Directory.Exists(CacheRoot)) Directory.Delete(CacheRoot, true); }
    }
}
