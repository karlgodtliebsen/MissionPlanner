using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Notifications;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class VehicleFirmwareUpdateTests
{
    [Theory]
    [InlineData(7, 1, "4.7.1", false)]
    [InlineData(7, 1, "4.7.2", true)]
    [InlineData(8, 0, "4.7.2", false)]
    [InlineData(7, 1, "4.10.0", true)]
    public void ComparesNumericStableVersions(byte minor, byte patch, string available, bool update)
    {
        var identity = Identity() with { FlightVersion = new(4, minor, patch, FirmwareReleaseType.Official) };
        Assert.Equal(update, FirmwareUpdatePolicy.FindUpdate(identity, "BETAFPV-F405", [Entry("BETAFPV-F405", available)]) is not null);
    }

    [Fact]
    public void SharedBoardIdNeverCollapsesExactPlatformsOrHelicopterVariants()
    {
        var expected = Entry("BETAFPV-F405");
        var entries = new[] { expected, Entry("BETAFPV-F405-heli"), Entry("BETAFPV-F405-I2C"), Entry("BETAFPV-F405-I2C-heli"),
            Entry("BETAFPV-F405", "9.0.0", FirmwareVehicleType.Helicopter) };
        Assert.Same(expected, FirmwareUpdatePolicy.FindUpdate(Identity(), "BETAFPV-F405", entries));
        Assert.Null(FirmwareUpdatePolicy.FindUpdate(Identity(), "BETAFPV", entries));
        Assert.Null(FirmwareUpdatePolicy.FindUpdate(Identity() with { Autopilot = 12 }, "BETAFPV-F405", entries));
        Assert.Null(FirmwareUpdatePolicy.FindUpdate(Identity() with { FlightVersion = new(4, 7, 1, FirmwareReleaseType.Beta) }, "BETAFPV-F405", entries));
    }

    [Fact]
    public async Task ReconnectSuppressesSameUpdateButNewReleaseCanNotify()
    {
        var (service, catalog, notifications) = Create();
        await using (service)
        {
            await service.Start(new(1, 1), DateTimeOffset.UnixEpoch);
            Assert.Equal("BETAFPV-F405", service.Current?.Available.Target.Platform);
            service.Cancel();
            await service.Start(new(1, 1), DateTimeOffset.UnixEpoch);
            await notifications.Received(1).NotifyAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());
            catalog.GetCatalogAsync(Arg.Any<FirmwareCatalogRequest>(), Arg.Any<CancellationToken>())
                .Returns(new FirmwareCatalog([Entry("BETAFPV-F405", "4.8.0")], DateTimeOffset.UtcNow, false));
            await service.Start(new(1, 1), DateTimeOffset.UnixEpoch);
            await notifications.Received(2).NotifyAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task CatalogFailureAndDisconnectDoNotEscapeBackgroundWork()
    {
        var (service, catalog, notifications) = Create();
        await using (service)
        {
            catalog.GetCatalogAsync(Arg.Any<FirmwareCatalogRequest>(), Arg.Any<CancellationToken>())
                .Returns<Task<FirmwareCatalog>>(_ => throw new IOException("offline"));
            await service.Start(new(1, 1), DateTimeOffset.UnixEpoch);
            Assert.Null(service.Current);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            catalog.GetCatalogAsync(Arg.Any<FirmwareCatalogRequest>(), Arg.Any<CancellationToken>())
                .Returns(async call =>
                {
                    entered.TrySetResult();
                    await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
                    return new FirmwareCatalog([], DateTimeOffset.UtcNow, false);
                });
            var check = service.Start(new(1, 1), DateTimeOffset.UnixEpoch);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(check.IsCompleted);
            service.Cancel();
            await check.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await notifications.DidNotReceive().NotifyAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task NonArduPilotSkipsCatalogAndMissingTargetNeverNotifies()
    {
        var (unsupported, unsupportedCatalog, _) = Create(autopilot: 12);
        await using (unsupported)
        {
            await unsupported.Start(new(1, 1), DateTimeOffset.UnixEpoch);
            await unsupportedCatalog.DidNotReceive().GetCatalogAsync(Arg.Any<FirmwareCatalogRequest>(), Arg.Any<CancellationToken>());
        }
        var clock = new MissionPlanner.Test.Support.ManualTimeProvider(DateTimeOffset.UtcNow);
        var (service, _, notifications) = Create(includeBanner: false, clock: clock);
        await using (service)
        {
            var check = service.Start(new(1, 1), DateTimeOffset.UnixEpoch);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (clock.TimerCount < 2)
            {
                await Task.Delay(1, timeout.Token);
            }
            clock.Advance(TimeSpan.FromSeconds(16));
            await check.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Null(service.Current);
            await notifications.DidNotReceive().NotifyAsync(Arg.Any<UserNotification>(), Arg.Any<CancellationToken>());
        }
    }

    private static (VehicleFirmwareUpdateService, IFirmwareCatalogService, IUserNotificationService) Create(bool includeBanner = true, byte autopilot = 3, TimeProvider? clock = null)
    {
        var registry = Substitute.For<IVehicleRegistry>();
        var messages = Substitute.For<IVehicleMessageStore>();
        var catalog = Substitute.For<IFirmwareCatalogService>();
        var notifications = Substitute.For<IUserNotificationService>();
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(new VehicleId(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
            VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
        state = state with { Identity = state.Identity with { Firmware = Identity() with { Autopilot = autopilot } } };
        registry.GetRequired(new(1, 1)).Returns(new VehicleSession(state, new TransportEndPoint("test"), Substitute.For<IDateTimeProvider>()));
        messages.GetMessages(new(1, 1)).Returns(includeBanner ? [new VehicleStatusText(new(1, 1), 1, 1, MissionPlanner.MavLink.MavSeverity.Info,
            "BETAFPV-F405 00340031 3438510F", now)] : []);
        catalog.GetCatalogAsync(Arg.Any<FirmwareCatalogRequest>(), Arg.Any<CancellationToken>())
            .Returns(new FirmwareCatalog([Entry("BETAFPV-F405")], now, false));
        return (new VehicleFirmwareUpdateService(registry, messages, catalog, Substitute.For<IDomainEventHub>(),
            notifications, clock ?? TimeProvider.System, NullLogger<VehicleFirmwareUpdateService>.Instance), catalog, notifications);
    }

    private static VehicleFirmwareIdentity Identity() => new(FirmwareFamily.ArduCopter, 2, 3,
        new(4, 7, 1, FirmwareReleaseType.Official), null, 0, 0, 0, 0, 42, null);

    private static FirmwareManifestEntry Entry(string platform, string version = "4.7.2", FirmwareVehicleType variant = FirmwareVehicleType.Copter) =>
        new(new FirmwareVersion(version, Version.Parse(version)), FirmwareReleaseChannel.Stable,
            new FirmwareBoardTarget(105, platform, FirmwareVehicleType.Copter, variant),
            new FirmwareArtifact(new Uri($"https://firmware.ardupilot.org/{platform}/{version}/firmware.apj"), FirmwareImageFormat.Apj));
}
