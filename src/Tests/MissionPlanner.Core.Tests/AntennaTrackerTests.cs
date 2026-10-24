using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Exercises serial wire formats and bounded tracker actuation with simulated transports.</summary>
public sealed class AntennaTrackerTests
{
    [Fact]
    public void DriversUseCalibratedLimitsAndCorrectWireUnits()
    {
        var p = new SerialTrackerProfile();
        Assert.Equal(new byte[] { 0x84, 0, 112, 46, 0x84, 1, 112, 46 }, SerialAntennaTracker.Encode(p, 0, 45));
        Assert.Equal("!!!PAN:2000,TLT:1000\n", Encoding.ASCII.GetString(SerialAntennaTracker.Encode(p with { Driver = SerialTrackerDriver.ArduTracker }, 999, -999)));
        Assert.Equal("!!!PAN:-0900,TLT:0900\n", Encoding.ASCII.GetString(SerialAntennaTracker.Encode(p with { Driver = SerialTrackerDriver.DegreeTracker, Pan = p.Pan with { Reversed = true } }, 90, 90)));
        Assert.Throws<ArgumentException>(() => SerialAntennaTracker.Encode(p, double.NaN, 0));
        Assert.Throws<ArgumentException>(() => (p with { TiltChannel = 0 }).Validate());
        Assert.Throws<ArgumentException>(() => (p with { Pan = p.Pan with { MinimumPwm = 700 } }).Validate());
        var pointing = SerialAntennaTracker.Pointing(new(0, 0), 10, (new(0, .001), 121.2), p, 0);
        Assert.Equal(90, pointing.Pan, 4); Assert.InRange(pointing.Tilt, 44.99, 45.01);
        Assert.Equal(35, SerialAntennaTracker.Pointing(new(0, 0), 10, (new(0, 0), 20), p, 35).Pan);
    }

    [Theory]
    [InlineData(SerialTrackerDriver.Maestro)]
    [InlineData(SerialTrackerDriver.ArduTracker)]
    public async Task CancellationClosesPortAndDisablesMaestroPulsesOnly(SerialTrackerDriver driver)
    {
        var active = Substitute.For<IActiveVehicleContext>();
        var factory = Substitute.For<IDirectSerialSessionFactory>();
        var port = Substitute.For<IDirectSerialSession>();
        var sent = new List<byte[]>();
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        factory.OpenAsync("fake", 9600, Arg.Any<CancellationToken>()).Returns(Task.FromResult(port));
        port.WriteBytesAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var bytes = call.Arg<ReadOnlyMemory<byte>>().ToArray(); sent.Add(bytes);
            if (driver != SerialTrackerDriver.Maestro || bytes.Length == 8 && bytes[2] != 0) lifetime.Cancel();
            return Task.CompletedTask;
        });
        var service = new SerialAntennaTracker(factory, active);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.TestAsync("fake", 9600, new() { Driver = driver }, 0, 45, true, new Progress<string>(), lifetime.Token));
        if (driver == SerialTrackerDriver.Maestro)
        {
            Assert.Equal(6, sent.Count);
            Assert.Equal(new byte[] { 0x84, 0, 0, 0, 0x84, 1, 0, 0 }, sent[^1]);
        }
        else Assert.Single(sent);
        await port.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task TrackingRefusesStaleAircraftBeforeOpeningAnyPort()
    {
        var f = new Fixture();
        f.State = f.State with { Connection = f.State.Connection with { LastHeartbeatAt = DateTimeOffset.UtcNow.AddSeconds(-5) } };
        var factory = Substitute.For<IDirectSerialSessionFactory>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SerialAntennaTracker(factory, f.Active).TrackAsync("fake", 9600, new(), new(55, 12), 10, new(1, 1), true, new Progress<string>(), TestContext.Current.CancellationToken));
        await factory.DidNotReceiveWithAnyArgs().OpenAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void ReviewUsesAssignedOutputAndRejectsChangedLimitsOrAircraftIdentity()
    {
        var f = new Fixture();
        var review = f.Service.Review(new(1, 1), 1);
        Assert.Equal("SERVO7", review.Output); Assert.Equal(1100, review.Minimum);
        f.Values["SERVO8_FUNCTION"] = new("SERVO8_FUNCTION", 71, MavParamType.Int16, 0, 5);
        Assert.Throws<InvalidOperationException>(() => f.Service.Review(new(1, 1), 1));
        f.Values.Remove("SERVO8_FUNCTION");
        f.State = f.State with { Flight = f.State.Flight with { IsArmed = true } };
        Assert.Throws<InvalidOperationException>(() => f.Service.Review(new(1, 1), 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TestCancellationStillRequestsStopAndRequiresHeartbeatConfirmation(bool stopRejected)
    {
        var f = new Fixture();
        var review = f.Service.Review(new(1, 1), 1);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        f.Connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask(Reply(call.Arg<ReadOnlyMemory<byte>>().Span[0])));
        async Task Reply(byte command)
        {
            // A foreign endpoint's rejection must not end this transaction.
            await f.Events.PublishAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, new CommandAckMessage(1, 1, new("foreign"), command, 2, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
            if (command == 176 && !stopRejected) f.State = f.State with { Flight = f.State.Flight with { CustomMode = 1, IsArmed = false }, Connection = f.State.Connection with { LastHeartbeatAt = DateTimeOffset.UtcNow } };
            await f.Events.PublishAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, new CommandAckMessage(1, 1, f.Endpoint, command, (byte)(command == 176 && stopRejected ? 2 : 0), DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
            if (command == 183) lifetime.Cancel();
        }
        var task = f.Service.TestAsync(review, 1500, true, new Progress<string>(), lifetime.Token);
        if (stopRejected) Assert.Contains("Could not confirm Tracker Stop", (await Assert.ThrowsAsync<IOException>(() => task)).Message);
        else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(new ushort[] { 183, 176 }, f.Commands.Select(command => command.Id));
        Assert.Equal(1, f.Commands[0].Values[0]); // Logical yaw, not physical SERVO7.
        Assert.Equal(new float[] { 1, 1, 0, 0, 0, 0, 0 }, f.Commands[1].Values);
        Assert.True(f.Gate.TryAcquire(new(1, 1), "after test", out var lease)); lease!.Dispose();
    }

    [Fact]
    public async Task StaleReviewOrUnconfirmedTestCannotSendCommands()
    {
        var f = new Fixture(); var review = f.Service.Review(new(1, 1), 1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.TestAsync(review, 1500, false, new Progress<string>(), TestContext.Current.CancellationToken));
        f.Values["SERVO7_MAX"] = f.Values["SERVO7_MAX"] with { Value = 1800 };
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.TestAsync(review, 1500, true, new Progress<string>(), TestContext.Current.CancellationToken));
        Assert.Empty(f.Commands);
    }

    private sealed class Fixture
    {
        internal readonly IActiveVehicleContext Active = Substitute.For<IActiveVehicleContext>();
        internal readonly IMavLinkConnection Connection = Substitute.For<IMavLinkConnection>();
        internal readonly EventHub Events = new(NullLogger<EventHub>.Instance);
        internal readonly TransportEndPoint Endpoint = new("tracker-test");
        internal readonly VehicleOperationGate Gate = new();
        internal readonly Dictionary<string, VehicleParameter> Values = [];
        internal readonly List<(ushort Id, float[] Values)> Commands = [];
        internal readonly AntennaTrackerTestService Service;
        internal VehicleState State;
        internal Fixture()
        {
            var now = DateTimeOffset.UtcNow;
            State = new(new(1, 1), 1, 5, 3, 0, 4, 3, VehicleConnectionState.Online, now, VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
            Active.IsOnline.Returns(true); Active.VehicleId.Returns(new VehicleId(1, 1)); Active.State.Returns(_ => State);
            var clock = Substitute.For<IDateTimeProvider>(); clock.UtcNow.Returns(now);
            var registry = Substitute.For<IVehicleRegistry>(); registry.GetRequired(new(1, 1)).Returns(new VehicleSession(State, Endpoint, clock));
            foreach (var (name, value) in new[] { ("SERVO7_FUNCTION", 71f), ("SERVO7_MIN", 1100f), ("SERVO7_MAX", 1900f), ("SERVO7_TRIM", 1500f) }) Values[name] = new(name, value, MavParamType.Int16, 0, 4);
            var parameters = Substitute.For<IVehicleParameterRegistry>(); parameters.GetAllParameters(new(1, 1)).Returns(Values);
            var encoder = Substitute.For<IMavLinkCommandEncoder>();
            encoder.EncodeCommandLong(1, 1, Arg.Any<ushort>(), Arg.Any<IReadOnlyList<float>>()).Returns(call =>
            {
                var id = call.Arg<ushort>(); Commands.Add((id, call.Arg<IReadOnlyList<float>>().ToArray())); return new byte[] { (byte)id };
            });
            Service = new(Active, registry, parameters, Connection, encoder, Events, Gate);
        }
    }
}
