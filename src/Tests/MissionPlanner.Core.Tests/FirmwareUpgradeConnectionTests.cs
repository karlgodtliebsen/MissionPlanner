using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Checks connected upgrade handoff and post-reboot reconnect safeguards.</summary>
public sealed class FirmwareUpgradeConnectionTests
{
    /// <summary>OS build banners must not be mistaken for a different board.</summary>
    [Theory]
    [InlineData("omnibusf4", false)]
    [InlineData("other-board", true)]
    public async Task HandoffUsesBoardUidBannerAndIgnoresChibios(string board, bool reject)
    {
        var fixture = new Fixture();
        fixture.Messages.GetMessages(fixture.Id).Returns([
            new(fixture.Id, 1, 1, MissionPlanner.MavLink.MavSeverity.Info, "ChibiOS: 4f34e217", DateTimeOffset.UtcNow),
            new(fixture.Id, 1, 1, MissionPlanner.MavLink.MavSeverity.Info, $"{board} 002E005B 33355109 34313432", DateTimeOffset.UtcNow)]);
        if (reject)
        {
            await Assert.ThrowsAsync<FirmwareCompatibilityException>(() =>
                fixture.Service.ReleaseAsync(new("COM11"), Release(), TestContext.Current.CancellationToken));
            await fixture.Connection.DidNotReceive().ReleaseForFirmwareUpgradeAsync(
                Arg.Any<VehicleId>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
        else
        {
            await fixture.Service.ReleaseAsync(new("COM11"), Release(), TestContext.Current.CancellationToken);
            await fixture.Connection.Received(1).ReleaseForFirmwareUpgradeAsync(
                fixture.Id, "COM11", Arg.Any<CancellationToken>());
        }
    }

    /// <summary>Handoff cannot release an armed or different serial controller.</summary>
    [Theory]
    [InlineData(true, "COM11")]
    [InlineData(false, "COM12")]
    public async Task UnsafeHandoffDoesNotReleaseConnection(bool armed, string port)
    {
        var fixture = new Fixture();
        fixture.Active.State.Returns(fixture.State with { Flight = fixture.State.Flight with { IsArmed = armed } });
        await Assert.ThrowsAsync<FirmwareConnectionConflictException>(() =>
            fixture.Service.ReleaseAsync(new(port), Release(), TestContext.Current.CancellationToken));
        await fixture.Connection.DidNotReceive().ReleaseForFirmwareUpgradeAsync(
            Arg.Any<VehicleId>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A transient startup failure retries the matched endpoint and verifies its identity.</summary>
    [Fact]
    public async Task ReconnectRetriesUntilApplicationStarts()
    {
        var fixture = new Fixture();
        fixture.Connection.ConnectSerialExclusiveAsync("COM11", 115200, Arg.Any<CancellationToken>())
            .Returns(new VehicleConnectionResult(false, null, null), new VehicleConnectionResult(true, fixture.Id, fixture.Session));
        var identity = await fixture.Service.ReconnectAsync(new("COM11"), Release(), null, TestContext.Current.CancellationToken);
        Assert.Equal((byte)1, identity.FlightVersion!.Patch);
        await fixture.Connection.Received(2).ConnectSerialExclusiveAsync("COM11", 115200, Arg.Any<CancellationToken>());
    }

    /// <summary>Reconnect must never replace an unrelated connection that became active.</summary>
    [Fact]
    public async Task ReconnectStopsWhenAnotherConnectionOwnsTheSession()
    {
        var fixture = new Fixture();
        fixture.Connection.IsConnected.Returns(true);
        fixture.Connection.ConnectSerialExclusiveAsync("COM11", 115200, Arg.Any<CancellationToken>())
            .Returns(new VehicleConnectionResult(false, null, null));
        await Assert.ThrowsAsync<FirmwareVerificationException>(() =>
            fixture.Service.ReconnectAsync(new("COM11"), Release(), null, TestContext.Current.CancellationToken));
        await fixture.Connection.Received(1).ConnectSerialExclusiveAsync("COM11", 115200, Arg.Any<CancellationToken>());
    }

    private static FirmwareManifestEntry Release() => new(new("4.7.1", new Version(4, 7, 1)),
        FirmwareReleaseChannel.Stable, new(1002, "omnibusf4", FirmwareVehicleType.Copter, FirmwareVehicleType.Copter),
        new(new Uri("https://example.test/firmware.apj"), FirmwareImageFormat.Apj));

    private sealed class Fixture
    {
        public VehicleId Id { get; } = new(1, 1);
        public IActiveVehicleContext Active { get; } = Substitute.For<IActiveVehicleContext>();
        public IVehicleConnectionService Connection { get; } = Substitute.For<IVehicleConnectionService>();
        public IVehicleConnectionSession Session { get; } = Substitute.For<IVehicleConnectionSession>();
        public IVehicleMessageStore Messages { get; } = Substitute.For<IVehicleMessageStore>();
        public VehicleState State { get; }
        public FirmwareUpgradeConnection Service { get; }

        public Fixture()
        {
            var state = new VehicleState(Id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, DateTimeOffset.UtcNow,
                VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
            State = state with { Identity = state.Identity with { Firmware = new(FirmwareFamily.ArduCopter, 2, 3,
                new(4, 7, 1, FirmwareReleaseType.Official), null, 0, 1002u << 16, 0x1209, 0x5741, 123, "UID") } };
            Active.VehicleId.Returns(Id);
            Active.State.Returns(State);
            Active.IsOnline.Returns(true);
            Session.ActiveSerialPort.Returns("COM11");
            Session.ActiveTransportProtocol.Returns("serial");
            Connection.ReleaseForFirmwareUpgradeAsync(Id, "COM11", Arg.Any<CancellationToken>()).Returns(true);
            Messages.GetMessages(Id).Returns([]);
            var registry = Substitute.For<IVehicleRegistry>();
            registry.GetRequired(Id).Returns(new VehicleSession(State, new TransportEndPoint("test"), Substitute.For<IDateTimeProvider>()));
            Service = new(Active, Connection, Session, registry, Messages, NullLogger<FirmwareUpgradeConnection>.Instance);
        }
    }
}
