using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Simulation;
using MissionPlanner.Core.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies safe SITL manifest selection, installation, and cache ownership.</summary>
[Trait("TestTier", "Unit")]
public sealed class SitlInstallationTests
{
    /// <summary>Verifies selection requires an exact family, channel, platform, and architecture match.</summary>
    [Fact]
    public void SelectorReturnsOnlyCompatibleReleasesNewestFirst()
    {
        var selected = new SitlReleaseSelector().Select(
            [
                Release("4.5.0", FirmwareFamily.ArduCopter, FirmwareReleaseChannel.Stable, SitlPlatform.Windows, SitlArchitecture.X64),
                Release("4.6.0", FirmwareFamily.ArduCopter, FirmwareReleaseChannel.Stable, SitlPlatform.Windows, SitlArchitecture.X64),
                Release("4.7.0", FirmwareFamily.ArduPlane, FirmwareReleaseChannel.Stable, SitlPlatform.Windows, SitlArchitecture.X64),
                Release("4.8.0", FirmwareFamily.ArduCopter, FirmwareReleaseChannel.Beta, SitlPlatform.Windows, SitlArchitecture.X64),
                Release("4.9.0", FirmwareFamily.ArduCopter, FirmwareReleaseChannel.Stable, SitlPlatform.Linux, SitlArchitecture.X64)
            ],
            FirmwareFamily.ArduCopter,
            FirmwareReleaseChannel.Stable,
            Capability());

        selected.Select(item => item.Version).Should().Equal("4.6.0", "4.5.0");
    }

    /// <summary>Verifies a package is installed only after its SHA-256 checksum succeeds.</summary>
    [Fact]
    public async Task PackageManagerVerifiesChecksumAndExtractsIntoVersionedCache()
    {
        using var cache = new TestCache();
        var archive = Zip(("bin/arducopter.exe", "verified SITL"));
        var release = Release("4.6.0", sha256: Hash(archive));
        var progress = new ImmediateProgress();
        var manager = PackageManager(cache.Path, archive);

        var installation = await manager.PrepareAsync(
            release,
            progress,
            TestContext.Current.CancellationToken);

        installation.State.Should().Be(SitlInstallationState.Available);
        installation.Source.Should().Be(SitlInstallationSource.VerifiedCache);
        File.ReadAllText(installation.ExecutablePath).Should().Be("verified SITL");
        installation.ExecutablePath.Should().StartWith(cache.Path);
        progress.Values.Should().Contain(1);
    }

    /// <summary>Verifies a checksum mismatch leaves no partially installed cache entry.</summary>
    [Fact]
    public async Task PackageManagerRejectsChecksumMismatchAtomically()
    {
        using var cache = new TestCache();
        var archive = Zip(("bin/arducopter.exe", "untrusted SITL"));
        var manager = PackageManager(cache.Path, archive);
        var release = Release("4.6.0", sha256: new string('0', 64));

        var action = () => manager.PrepareAsync(release, cancellationToken: TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*checksum mismatch*");
        (await manager.DiscoverCachedAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        Directory.Exists(Path.Combine(cache.Path, "staging")).Should().BeTrue();
        Directory.EnumerateDirectories(Path.Combine(cache.Path, "staging")).Should().BeEmpty();
    }

    /// <summary>Verifies archive traversal is rejected and cannot write outside the staging root.</summary>
    [Fact]
    public async Task PackageManagerRejectsArchivePathTraversal()
    {
        using var cache = new TestCache();
        var archive = Zip(("../escaped.exe", "escape"), ("bin/arducopter.exe", "SITL"));
        var manager = PackageManager(cache.Path, archive);
        var release = Release("4.6.0", sha256: Hash(archive));
        var escapedPath = Path.Combine(cache.Path, "escaped.exe");

        var action = () => manager.PrepareAsync(release, cancellationToken: TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*escapes*");
        File.Exists(escapedPath).Should().BeFalse();
        (await manager.DiscoverCachedAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    /// <summary>Verifies pruning retains pinned and recent cache entries and never removes external binaries.</summary>
    [Fact]
    public async Task CacheLifecycleRetainsPinsAndCannotRemoveExternalInstallations()
    {
        using var cache = new TestCache();
        var archive = Zip(("bin/arducopter.exe", "SITL"));
        var manager = PackageManager(cache.Path, archive);
        var first = await manager.PrepareAsync(
            Release("4.4.0", sha256: Hash(archive), publishedAt: new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            cancellationToken: TestContext.Current.CancellationToken);
        var second = await manager.PrepareAsync(
            Release("4.5.0", sha256: Hash(archive), publishedAt: new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero)),
            cancellationToken: TestContext.Current.CancellationToken);
        var third = await manager.PrepareAsync(
            Release("4.6.0", sha256: Hash(archive), publishedAt: new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero)),
            cancellationToken: TestContext.Current.CancellationToken);

        var removed = await manager.PruneAsync(
            new HashSet<string>([first.InstallationId]),
            1,
            TestContext.Current.CancellationToken);

        removed.Should().ContainSingle().Which.Should().Be(second.InstallationId);
        (await manager.DiscoverCachedAsync(TestContext.Current.CancellationToken))
            .Select(item => item.InstallationId)
            .Should().BeEquivalentTo(first.InstallationId, third.InstallationId);
        var external = first with
        {
            InstallationId = "external-test",
            Source = SitlInstallationSource.External,
            CacheKey = null,
            ExecutablePath = Path.Combine(cache.Path, "user-owned.exe")
        };
        await FluentActions.Awaiting(() => manager.RemoveAsync(external, TestContext.Current.CancellationToken))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*cannot be removed*");
    }

    /// <summary>Verifies unavailable platforms expose no installable release and reject installation.</summary>
    [Fact]
    public async Task UnsupportedPlatformCannotSelectOrInstallRelease()
    {
        var capability = new SitlPlatformCapability(
            SitlPlatform.MacOS,
            SitlArchitecture.Arm64,
            false,
            "Unavailable in this application host.");
        new SitlReleaseSelector().Select(
                [Release("4.6.0")],
                FirmwareFamily.ArduCopter,
                FirmwareReleaseChannel.Stable,
                capability)
            .Should().BeEmpty();
        var platform = Substitute.For<ISitlPlatformService>();
        platform.Current.Returns(capability);
        var packageManager = Substitute.For<ISitlPackageManager>();
        var service = new SitlInstallationService(
            Substitute.For<ISitlManifestProvider>(),
            new SitlReleaseSelector(),
            platform,
            packageManager,
            Options.Create(new SitlManifestOptions()),
            Substitute.For<ILogger<SitlInstallationService>>());

        var action = () => service.InstallAsync(Release("4.6.0"), cancellationToken: TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<PlatformNotSupportedException>();
        await packageManager.DidNotReceive().PrepareAsync(
            Arg.Any<SitlManifestEntry>(),
            Arg.Any<IProgress<double>?>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies a profile resolves by exact installation identity and reports missing pins.</summary>
    [Fact]
    public void ProfileResolutionUsesExactInstallationPin()
    {
        var packageManager = Substitute.For<ISitlPackageManager>();
        var service = new SitlInstallationService(
            Substitute.For<ISitlManifestProvider>(),
            new SitlReleaseSelector(),
            PlatformService(Capability()),
            packageManager,
            Options.Create(new SitlManifestOptions()),
            Substitute.For<ILogger<SitlInstallationService>>());
        var installation = new SitlInstallation(
            "cached-exact",
            FirmwareFamily.ArduCopter,
            SitlPlatform.Windows,
            SitlArchitecture.X64,
            "4.6.0",
            Path.GetFullPath("arducopter.exe"),
            SitlInstallationSource.VerifiedCache,
            SitlInstallationState.Available,
            "exact",
            "Available.");
        var profile = SimulatorProfile.CreateDefault() with
        {
            FirmwareFamily = FirmwareFamily.ArduCopter,
            Binary = new SimulatorBinaryReference(
                "4.6.0",
                installation.ExecutablePath,
                "VerifiedCache",
                installation.InstallationId)
        };

        service.Resolve(profile, [installation]).Installation.Should().BeSameAs(installation);
        service.Resolve(
            profile with { Binary = profile.Binary with { InstallationId = "cached-missing" } },
            [installation]).State.Should().Be(SitlInstallationState.Missing);
        service.Resolve(
            profile with
            {
                Binary = profile.Binary with
                {
                    InstallationId = "cached-missing",
                    Version = "9.9.9",
                    ExecutablePath = Path.GetFullPath("missing.exe")
                }
            },
            [installation]).State.Should().Be(SitlInstallationState.Missing);
    }

    private static ISitlPlatformService PlatformService(SitlPlatformCapability capability)
    {
        var service = Substitute.For<ISitlPlatformService>();
        service.Current.Returns(capability);
        return service;
    }

    private static SitlPlatformCapability Capability() =>
        new(SitlPlatform.Windows, SitlArchitecture.X64, true, "Supported test platform.");

    private static SitlManifestEntry Release(
        string version,
        FirmwareFamily family = FirmwareFamily.ArduCopter,
        FirmwareReleaseChannel channel = FirmwareReleaseChannel.Stable,
        SitlPlatform platform = SitlPlatform.Windows,
        SitlArchitecture architecture = SitlArchitecture.X64,
        string? sha256 = null,
        DateTimeOffset? publishedAt = null) =>
        new(
            family,
            platform,
            architecture,
            version,
            channel,
            new Uri($"https://firmware.example.test/{family}/{version}.zip"),
            sha256 ?? new string('a', 64),
            SitlArchiveFormat.Zip,
            "bin/arducopter.exe",
            publishedAt ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static SitlPackageManager PackageManager(string cachePath, byte[] archive)
    {
        var httpClient = new HttpClient(new ArchiveHandler(archive));
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("SITL").Returns(httpClient);
        var cache = Substitute.For<ISitlCachePathProvider>();
        cache.CacheRoot.Returns(cachePath);
        return new SitlPackageManager(
            factory,
            cache,
            Options.Create(new SitlManifestOptions()),
            Substitute.For<ILogger<SitlPackageManager>>());
    }

    private static byte[] Zip(params (string Path, string Content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var item in entries)
            {
                var entry = archive.CreateEntry(item.Path);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(item.Content);
            }
        }

        return buffer.ToArray();
    }

    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private sealed class ArchiveHandler(byte[] archive) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(archive),
                RequestMessage = request
            });
        }
    }

    private sealed class ImmediateProgress : IProgress<double>
    {
        public List<double> Values { get; } = [];

        public void Report(double value) => Values.Add(value);
    }

    private sealed class TestCache : IDisposable
    {
        public TestCache()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "MissionPlannerTests",
                "Sitl",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
