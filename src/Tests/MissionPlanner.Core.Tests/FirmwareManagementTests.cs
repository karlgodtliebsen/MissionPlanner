using System.Net;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.InitSetup;
using MissionPlanner.App.Views.InitSetup.Tabs;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies safe firmware identity, manifest, package, and flashing workflows.</summary>
public sealed class FirmwareManagementTests
{
    /// <summary>Verifies all protocol-reported identity fields are formatted for the firmware page.</summary>
    [Fact]
    public void FirmwareViewModelFormatsCompleteIdentity()
    {
        var state = State();
        var active = new TestActiveVehicleContext(state);
        var coordinator = Substitute.For<IFirmwareUpdateCoordinator>();
        var flashing = Substitute.For<IFirmwareFlashingService>();
        flashing.GetPlatformSupport(state.Identity.Firmware).Returns(new FirmwareFlashSupport(false, "adapter unavailable"));
        using var viewModel = new FirmwareSetupViewModel(
            new SetupWorkflowCatalog().Workflows[0],
            active,
            coordinator,
            flashing,
            Substitute.For<IUserConfirmationService>(),
            ImmediateDispatcher(),
            Substitute.For<ILogger<FirmwareSetupViewModel>>());

        viewModel.VehicleLabel.Should().Be(state.DisplayName);
        viewModel.FirmwareVersion.Should().Contain("4.5.6-beta");
        viewModel.ReleaseType.Should().Be("Beta");
        viewModel.GitHash.Should().Be("abcdef12");
        viewModel.BoardVersion.Should().Contain("0x0000002A");
        viewModel.VendorProduct.Should().Contain("0x1209").And.Contain("0x5740");
        viewModel.HardwareUid.Should().Be("0000000000001234");
        viewModel.HardwareUid2.Should().Be("00112233445566778899aabbccddeeff");
        viewModel.MavLinkVersion.Should().Be("3");
        viewModel.Capabilities.Should().Contain("Ftp");
        viewModel.FlashingAvailability.Should().Be("adapter unavailable");
    }

    /// <summary>Verifies manifest selection requires exact family and technical board identifiers.</summary>
    [Fact]
    public void ManifestSelectorDoesNotInferBoardFromNames()
    {
        var identity = State().Identity.Firmware;
        var selector = new FirmwareManifestSelector();
        var matching = Release(FirmwareReleaseChannel.Stable);
        var wrongProduct = matching with { ProductId = 1, BoardTarget = "Marketing Name Match" };
        var wrongFamily = matching with { Family = FirmwareFamily.ArduPlane };
        var beta = matching with { Channel = FirmwareReleaseChannel.Beta, Version = "beta" };

        var result = selector.Select([wrongProduct, wrongFamily, beta, matching], identity, FirmwareReleaseChannel.Stable);

        result.Should().ContainSingle().Which.Should().Be(matching);
        selector.Select([matching], identity with { VendorId = 0, ProductId = 0 }, FirmwareReleaseChannel.Stable).Should().BeEmpty();
    }

    /// <summary>Verifies remote JSON manifests accept documented string enum values.</summary>
    [Fact]
    public async Task ManifestProviderReadsStringChannelsAndFamilies()
    {
        const string json = """
                            [{
                              "family":"ArduCopter",
                              "boardTarget":"fmuv6c",
                              "vendorId":4617,
                              "productId":22336,
                              "boardVersion":42,
                              "version":"4.5.6",
                              "channel":"Stable",
                              "downloadUri":"https://firmware.example.test/fmuv6c.bin",
                              "sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                              "releaseNotes":"notes",
                              "publishedAt":"2026-07-22T10:00:00Z"
                            }]
                            """;
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Firmware").Returns(new HttpClient(new StaticContentHandler(System.Text.Encoding.UTF8.GetBytes(json))));
        var provider = new JsonFirmwareManifestProvider(
            factory,
            Options.Create(new FirmwareManifestOptions { ManifestUrl = "https://firmware.example.test/manifest.json" }),
            Substitute.For<ILogger<JsonFirmwareManifestProvider>>());

        var releases = await provider.GetReleasesAsync(TestContext.Current.CancellationToken);

        releases.Should().ContainSingle();
        releases[0].Family.Should().Be(FirmwareFamily.ArduCopter);
        releases[0].Channel.Should().Be(FirmwareReleaseChannel.Stable);
    }

    /// <summary>Verifies firmware downloads are cached only after a matching SHA-256 digest.</summary>
    [Fact]
    public async Task PackageManagerVerifiesChecksumBeforeCaching()
    {
        var content = "verified firmware"u8.ToArray();
        var release = Release(FirmwareReleaseChannel.Stable) with { Sha256 = Hash(content) };
        var cache = new MemoryPackageCache();
        var manager = CreatePackageManager(content, cache);

        var package = await manager.PrepareAsync(release, cancellationToken: TestContext.Current.CancellationToken);

        package.IsVerified.Should().BeTrue();
        package.Sha256.Should().Be(release.Sha256);
        cache.Saved.Should().BeTrue();
    }

    /// <summary>Verifies checksum mismatches fail closed and never populate the cache.</summary>
    [Fact]
    public async Task PackageManagerRejectsChecksumMismatch()
    {
        var cache = new MemoryPackageCache();
        var manager = CreatePackageManager("tampered"u8.ToArray(), cache);

        var action = () => manager.PrepareAsync(Release(FirmwareReleaseChannel.Stable), cancellationToken: TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidDataException>();
        cache.Saved.Should().BeFalse();
    }

    /// <summary>Verifies verified packages disconnect MAVLink, flash, and complete only after reconnect.</summary>
    [Fact]
    public async Task CoordinatorEnforcesVerifyDisconnectFlashReconnectOrder()
    {
        var state = State();
        var active = new TestActiveVehicleContext(state);
        var release = Release(FirmwareReleaseChannel.Stable);
        var package = new FirmwarePackage(release, "cached.bin", release.Sha256, true);
        var provider = Substitute.For<IFirmwareManifestProvider>();
        provider.GetReleasesAsync(Arg.Any<CancellationToken>()).Returns([release]);
        var packages = Substitute.For<IFirmwarePackageManager>();
        packages.PrepareAsync(release, Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>()).Returns(package);
        var flashing = Substitute.For<IFirmwareFlashingService>();
        flashing.GetPlatformSupport(state.Identity.Firmware).Returns(new FirmwareFlashSupport(true, "supported"));
        flashing.GetSupport(state.Identity.Firmware, package).Returns(new FirmwareFlashSupport(true, "supported"));
        flashing.FlashAsync(Arg.Any<FirmwareFlashRequest>(), Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>())
            .Returns(new FirmwareFlashResult(true, "flashed"));
        var connection = Substitute.For<IVehicleConnectionService>();
        connection.DisconnectAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            active.Set(state with { Connection = state.Connection with { State = VehicleConnectionState.Offline } });
            return Task.CompletedTask;
        });
        using var coordinator = new FirmwareUpdateCoordinator(
            provider,
            new FirmwareManifestSelector(),
            packages,
            flashing,
            connection,
            active,
            Substitute.For<ILogger<FirmwareUpdateCoordinator>>());

        (await coordinator.DiscoverAsync(state.Identity.Firmware, FirmwareReleaseChannel.Stable, TestContext.Current.CancellationToken)).Should().ContainSingle();
        await coordinator.PrepareAsync(release, TestContext.Current.CancellationToken);
        var result = await coordinator.FlashAsync(state.VehicleId, state.Identity.Firmware, true, TestContext.Current.CancellationToken);

        result.Succeeded.Should().BeTrue();
        coordinator.State.Should().Be(FirmwareUpdateState.WaitingForReconnect);
        await connection.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
        await flashing.Received(1).FlashAsync(Arg.Any<FirmwareFlashRequest>(), Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>());

        active.Set(state);
        coordinator.State.Should().Be(FirmwareUpdateState.Completed);
    }

    /// <summary>Verifies unverified packages and unsupported platforms are blocked before disconnect.</summary>
    [Fact]
    public async Task CoordinatorBlocksUnsafeOrUnsupportedFlash()
    {
        var state = State();
        var active = new TestActiveVehicleContext(state);
        var connection = Substitute.For<IVehicleConnectionService>();
        var flashing = new UnsupportedFirmwareFlashingService();
        var release = Release(FirmwareReleaseChannel.Stable);
        var package = new FirmwarePackage(release, "cached.bin", release.Sha256, true);
        var packages = Substitute.For<IFirmwarePackageManager>();
        packages.PrepareAsync(release, Arg.Any<IProgress<double>?>(), Arg.Any<CancellationToken>()).Returns(package);
        using var coordinator = new FirmwareUpdateCoordinator(
            Substitute.For<IFirmwareManifestProvider>(),
            new FirmwareManifestSelector(),
            packages,
            flashing,
            connection,
            active,
            Substitute.For<ILogger<FirmwareUpdateCoordinator>>());

        var beforeVerification = await coordinator.FlashAsync(state.VehicleId, state.Identity.Firmware, true, TestContext.Current.CancellationToken);

        beforeVerification.Succeeded.Should().BeFalse();
        beforeVerification.Message.Should().Contain("verified");
        await connection.DidNotReceive().DisconnectAsync(Arg.Any<CancellationToken>());

        await coordinator.PrepareAsync(release, TestContext.Current.CancellationToken);
        var unsupported = await coordinator.FlashAsync(state.VehicleId, state.Identity.Firmware, true, TestContext.Current.CancellationToken);
        unsupported.Succeeded.Should().BeFalse();
        unsupported.Message.Should().Contain("no platform bootloader adapter");
        await connection.DidNotReceive().DisconnectAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies cancellation is reflected explicitly by the discovery state machine.</summary>
    [Fact]
    public async Task CoordinatorReportsDiscoveryCancellation()
    {
        var provider = Substitute.For<IFirmwareManifestProvider>();
        provider.GetReleasesAsync(Arg.Any<CancellationToken>()).Returns(call =>
            WaitForCancellationAsync(call.Arg<CancellationToken>()));
        var active = new TestActiveVehicleContext(State());
        using var coordinator = new FirmwareUpdateCoordinator(
            provider,
            new FirmwareManifestSelector(),
            Substitute.For<IFirmwarePackageManager>(),
            new UnsupportedFirmwareFlashingService(),
            Substitute.For<IVehicleConnectionService>(),
            active,
            Substitute.For<ILogger<FirmwareUpdateCoordinator>>());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => coordinator.DiscoverAsync(active.State!.Identity.Firmware, FirmwareReleaseChannel.Stable, cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        coordinator.State.Should().Be(FirmwareUpdateState.Cancelled);
    }

    private static FirmwarePackageManager CreatePackageManager(byte[] content, MemoryPackageCache cache)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("Firmware").Returns(new HttpClient(new StaticContentHandler(content)));
        return new FirmwarePackageManager(factory, cache, Substitute.For<ILogger<FirmwarePackageManager>>());
    }

    private static async Task<IReadOnlyList<FirmwareManifestEntry>> WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return [];
    }

    private static string Hash(byte[] value)
    {
        return Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    }

    private static FirmwareManifestEntry Release(FirmwareReleaseChannel channel)
    {
        return new FirmwareManifestEntry(
            FirmwareFamily.ArduCopter,
            "fmuv6c",
            0x1209,
            0x5740,
            42,
            "4.5.6",
            channel,
            new Uri("https://firmware.example.test/fmuv6c.bin"),
            new string('a', 64),
            "Release notes",
            DateTimeOffset.Parse("2026-07-22T10:00:00Z"));
    }

    private static VehicleState State()
    {
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(new VehicleId(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now, VehicleMode.Stabilize,
            false, null, null, null, null, null, null, null, null);
        var firmware = new VehicleFirmwareIdentity(
            FirmwareFamily.ArduCopter,
            2,
            3,
            new FirmwareSemanticVersion(4, 5, 6, FirmwareReleaseType.Beta),
            "abcdef12",
            (ulong)(MavProtocolCapability.Ftp | MavProtocolCapability.ParamFloat),
            42,
            0x1209,
            0x5740,
            0x1234,
            "00112233445566778899aabbccddeeff");
        return state with { Identity = state.Identity with { Firmware = firmware } };
    }

    private static IDispatcher ImmediateDispatcher()
    {
        var dispatcher = Substitute.For<IDispatcher>();
        dispatcher.Dispatch(Arg.Any<Action>()).Returns(call =>
        {
            call.Arg<Action>()!();
            return true;
        });
        return dispatcher;
    }

    private sealed class StaticContentHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
        }
    }

    private sealed class MemoryPackageCache : IFirmwarePackageCache
    {
        private byte[]? value;

        public bool Saved { get; private set; }

        public string GetPath(string cacheKey)
        {
            return cacheKey;
        }

        public Task<Stream?> OpenReadAsync(string cacheKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Stream?>(value is null ? null : new MemoryStream(value, false));
        }

        public async Task<string> SaveAsync(string cacheKey, Stream content, CancellationToken cancellationToken = default)
        {
            await using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            value = buffer.ToArray();
            Saved = true;
            return cacheKey;
        }

        public void Remove(string cacheKey)
        {
            value = null;
        }
    }

    private sealed class TestActiveVehicleContext(VehicleState state) : IActiveVehicleContext
    {
        private CancellationTokenSource lifetime = new();

        public ActiveVehicleSnapshot Current { get; private set; } = new(state.VehicleId, state);

        public VehicleId? VehicleId => Current.VehicleId;

        public VehicleState? State => Current.State;

        public bool IsOnline => Current.IsOnline;

        public CancellationToken ConnectionCancellationToken => lifetime.Token;

        public event EventHandler<ActiveVehicleChangedEventArgs>? Changed;

        public void Set(VehicleState next)
        {
            var previous = Current;
            if (previous.IsOnline != (next.ConnectionState == VehicleConnectionState.Online))
            {
                lifetime.Cancel();
                lifetime.Dispose();
                lifetime = new CancellationTokenSource();
                if (next.ConnectionState != VehicleConnectionState.Online)
                {
                    lifetime.Cancel();
                }
            }

            Current = new ActiveVehicleSnapshot(next.VehicleId, next);
            Changed?.Invoke(this, new ActiveVehicleChangedEventArgs(previous, Current));
        }
    }
}
