using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;
using MissionPlanner.Core.Missions.Rally;
using MissionPlanner.Core.Missions.Transfer;
using MissionPlanner.Core.Setup.Advanced.Nmea;
using MissionPlanner.Core.Setup.Advanced.Position;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Missions;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Tests external GPS validity and connection-bound navigation without real hardware.</summary>
public sealed class OperatorPositionTests
{
    private static string Gga(DateTimeOffset now, string quality = "1", string latitude = "5500.0000", string hdop = "0.9") =>
        NmeaFormatter.Sentence($"GNGGA,{now.UtcDateTime.ToString("HHmmss.fff", CultureInfo.InvariantCulture)},{latitude},N,01200.0000,E,{quality},08,{hdop},30.0,M,0.0,M,,");

    [Fact]
    public void FixValidationRejectsCorruptionPoorQualityStaleTimeAndInvalidMinutes()
    {
        var now = DateTimeOffset.Parse("2026-10-24T00:00:01Z");
        var valid = OperatorNmeaParser.Parse(Gga(now.AddSeconds(-2)).Trim(), now)!;
        Assert.True(valid.IsFresh(now));
        Assert.Equal(55, valid.Position.LatitudeDegrees);
        Assert.Equal(30, valid.AltitudeMsl);
        Assert.False(valid.IsFresh(now.AddSeconds(4)));
        Assert.False(OperatorNmeaParser.Parse(Gga(now, "0").Trim(), now)!.Valid);
        Assert.False(OperatorNmeaParser.Parse(Gga(now, latitude: "5561.0000").Trim(), now)!.Valid);
        Assert.False(OperatorNmeaParser.Parse(Gga(now, hdop: "9").Trim(), now)!.Valid);
        Assert.False(OperatorNmeaParser.Parse(Gga(now.AddMinutes(-1)).Trim(), now)!.Valid);
        Assert.False(OperatorNmeaParser.Parse(Gga(now).Trim()[..^2] + "FF", now)!.Valid);
        Assert.InRange(OperatorNavigationService.Distance(new(0, 179.999), new(0, -179.999)), 200, 230);
    }

    [Fact]
    public async Task FollowSendsOnlyAfterExplicitStartAndRequestsHoldOnStop()
    {
        var f = new Fixture(false);
        using var inputLifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var inputTask = f.Input.RunAsync(new(RtkSourceKind.Tcp, "localhost", 14551), new Progress<OperatorPositionFix>(), inputLifetime.Token);
        f.Source.Push(Gga(f.Now));
        await f.WaitForFix();
        Assert.Empty(f.Packets);
        using var followLifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>()).Returns(_ => new ValueTask(Reply()));
        async Task Reply()
        {
            await f.Events.PublishAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, new CommandAckMessage(1, 1, f.Endpoint, 192, 0, f.Now), TestContext.Current.CancellationToken);
            sent.TrySetResult();
        }
        var follow = f.Service.FollowAsync(new(1, 1), 20, 100, true, new Progress<string>(), followLifetime.Token);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
        var command = Assert.IsType<CommandIntMessage>(Assert.Single(f.Packets));
        Assert.Equal(192, command.Command); Assert.Equal(0, command.Param2); Assert.Equal(6, command.Frame);
        Assert.Equal(550000000, command.X); Assert.Equal(20, command.Z);
        followLifetime.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => follow);
        await f.Commands.Received(1).HoldAsync(new(1, 1), Arg.Any<CancellationToken>());
        inputLifetime.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inputTask);
        Assert.Null(f.Input.Current);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MovingBasePreservesOtherRallyPointsAndRejectsConcurrentEdits(bool conflict)
    {
        var f = new Fixture(true);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        lifetime.CancelAfter(TimeSpan.FromSeconds(5));
        var inputTask = f.Input.RunAsync(new(RtkSourceKind.Serial, "test", 9600), new Progress<OperatorPositionFix>(), lifetime.Token);
        f.Source.Push(Gga(f.Now)); await f.WaitForFix();
        var original = new RallyPlan([new(RallyPointId.New(), new(54, 11), new(100, MissionAltitudeReference.Home)), new(RallyPointId.New(), new(55, 12), new(200, MissionAltitudeReference.MeanSeaLevel))]);
        var mapper = new RallyProtocolMapper();
        IReadOnlyList<MavLinkMissionItem> onboard = mapper.ToProtocol(original);
        var uploaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Transfers.DownloadAsync(new(1, 1), MissionPlanType.RallyPoints, Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(new MissionDownloadResult(true, onboard, null)));
        f.Transfers.UploadItemsAsync(new(1, 1), Arg.Any<IReadOnlyList<MavLinkMissionItem>>(), MissionPlanType.RallyPoints, Arg.Any<IProgress<MissionUploadProgress>?>(), Arg.Any<CancellationToken>())
            .Returns(call => { onboard = call.Arg<IReadOnlyList<MavLinkMissionItem>>(); uploaded.TrySetResult(); return Task.FromResult(new MissionUploadResult(true, 0, null)); });
        var review = await f.Service.ReadRallyAsync(new(1, 1), lifetime.Token);
        if (conflict) onboard = mapper.ToProtocol(new([original.Points[0] with { Position = new(53, 10) }, original.Points[1]]));
        var move = f.Service.MoveBaseAsync(review, 1, 50, true, new Progress<string>(), lifetime.Token);
        if (conflict)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => move);
            Assert.False(uploaded.Task.IsCompleted);
        }
        else
        {
            await uploaded.Task.WaitAsync(TimeSpan.FromSeconds(3), lifetime.Token);
            var plan = mapper.FromProtocol(onboard);
            Assert.Equal(original.Points[0].Position, plan.Points[0].Position);
            Assert.Equal(original.Points[0].Altitude, plan.Points[0].Altitude);
            Assert.Equal(new GeoPosition(55, 12), plan.Points[1].Position);
            Assert.Equal(new MissionAltitude(80, MissionAltitudeReference.MeanSeaLevel), plan.Points[1].Altitude);
            lifetime.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => move);
        }
        lifetime.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => inputTask);
    }

    private sealed class Fixture
    {
        internal readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
        internal readonly TransportEndPoint Endpoint = new("operator-test");
        internal readonly IActiveVehicleContext Active = Substitute.For<IActiveVehicleContext>();
        internal readonly IVehicleRegistry Vehicles = Substitute.For<IVehicleRegistry>();
        internal readonly IMavLinkConnection Connection = Substitute.For<IMavLinkConnection>();
        internal readonly IVehicleCommandService Commands = Substitute.For<IVehicleCommandService>();
        internal readonly IMissionTransferService Transfers = Substitute.For<IMissionTransferService>();
        internal readonly EventHub Events = new(NullLogger<EventHub>.Instance);
        internal readonly TestSource Source = new();
        internal readonly OperatorPositionInput Input;
        internal readonly OperatorNavigationService Service;
        internal readonly List<GeneratedMavLinkMessage> Packets = [];
        internal Fixture(bool plane)
        {
            var clock = Substitute.For<IDateTimeProvider>(); clock.UtcNow.Returns(Now);
            var state = new VehicleState(new(1, 1), plane ? 15u : 4u, plane ? (byte)1 : (byte)2, 3, 0, 4, 3, VehicleConnectionState.Online,
                Now, VehicleMode.Unknown, true, null, null, null, null, null, null, null, null);
            state = state with { Gps = state.Gps with { FixType = GpsFixType.Fix3D, LatitudeDegrees = 55, LongitudeDegrees = 12, ObservedAt = Now } };
            Active.IsOnline.Returns(true); Active.VehicleId.Returns(new VehicleId(1, 1)); Active.State.Returns(state);
            Vehicles.GetRequired(new(1, 1)).Returns(new VehicleSession(state, Endpoint, clock));
            var sources = Substitute.For<IRtkCorrectionSourceFactory>(); sources.OpenAsync(Arg.Any<RtkSourceOptions>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<IRtkCorrectionSource>(Source));
            Input = new(sources);
            var encoder = Substitute.For<IMavLinkWireMessageEncoder>(); encoder.Encode(Arg.Any<GeneratedMavLinkMessage>()).Returns(call => { Packets.Add(call.Arg<GeneratedMavLinkMessage>()); return new byte[] { 1 }; });
            Commands.HoldAsync(new(1, 1), Arg.Any<CancellationToken>()).Returns(Task.FromResult(new VehicleCommandResponse(new(1, 1), VehicleCommandResult.Accepted, Now)));
            var workspace = Substitute.For<IRallyConfigurationService>(); workspace.GetSnapshot(new(1, 1)).Returns(new RallyPlanSnapshot(new(1, 1), RallyPlan.Empty, null, 0, null, false, null));
            Service = new(Input, Active, Vehicles, Connection, encoder, Events, new VehicleOperationGate(), Commands, new TrackerHomeService(), Transfers, new RallyProtocolMapper(), workspace);
        }
        internal async Task WaitForFix()
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(3));
            while (Input.Current is null) await Task.Delay(10, timeout.Token);
            Assert.True(Input.Current.Valid);
        }
    }

    private sealed class TestSource : IRtkCorrectionSource
    {
        private readonly Channel<byte[]> packets = Channel.CreateUnbounded<byte[]>();
        internal void Push(string text) => packets.Writer.TryWrite(Encoding.ASCII.GetBytes(text));
        public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            var packet = await packets.Reader.ReadAsync(cancellationToken); packet.CopyTo(buffer); return packet.Length;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
