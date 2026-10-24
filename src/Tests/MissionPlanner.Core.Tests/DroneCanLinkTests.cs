using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Setup.OptionalHardware.DroneCan;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies adapter framing, target isolation and resource cleanup through simulated serial/MAVLink boundaries.</summary>
public sealed class DroneCanLinkTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Fact]
    public async Task SlcanConfiguresExplicitRateParsesIncomingFramesAndClosesChannel()
    {
        var port = new Serial(); var factory = Substitute.For<IDirectSerialSessionFactory>();
        factory.OpenAsync("fake", 115200, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IDirectSerialSession>(port));
        await using var link = new SlcanFrameLink(factory, new() { Port = "fake", CanBitRate = 500000 });
        await link.OpenAsync(Token); Assert.Equal(new[] { "C\r", "S6\r", "O\r" }, port.Commands);
        await link.SendAsync(new(0x1801557E, [1, 2, 3]), Token); Assert.Equal("T1801557E3010203\r", port.Commands.Last());
        port.Replies.Writer.TryWrite(Encoding.ASCII.GetBytes("T1801552A2AA")); port.Replies.Writer.TryWrite(Encoding.ASCII.GetBytes("BBCDEF\r"));
        var frame = await link.ReadAsync(Token); Assert.Equal(new byte[] { 0xAA, 0xBB }, frame.Data);
        await link.DisposeAsync(); Assert.True(port.Disposed); Assert.Equal("C\r", port.Commands.Last());
    }
    [Fact]
    public async Task SlcanRejectedCommandReleasesPortWithoutReportingOpen()
    {
        var port = new Serial { RejectOpen = true }; var factory = Substitute.For<IDirectSerialSessionFactory>();
        factory.OpenAsync("fake", 115200, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IDirectSerialSession>(port));
        await using var link = new SlcanFrameLink(factory, new() { Port = "fake" });
        await Assert.ThrowsAsync<IOException>(() => link.OpenAsync(Token)); Assert.True(port.Disposed);
    }
    [Fact]
    public async Task TunnelFiltersRouteBusTargetAndFlagsAndReleasesLease()
    {
        var f = new Fixture(); await using var link = f.Link;
        await link.OpenAsync(Token); Assert.Equal(2f, f.BusRequests.First());
        Assert.NotNull(f.Gate.GetCurrentOperation(new(1, 1)));
        var filter = Assert.IsType<CanFilterModifyMessage>(Assert.Single(f.WireMessages)); Assert.Equal((byte)2, filter.Bus); Assert.Equal((byte)0, filter.NumIds);
        var packet = new CanFrameMessage(1, 1, f.Endpoint, 255, 190, 1, 2, 0x9801552A, [1, 2], DateTimeOffset.UtcNow);
        await f.Publish(packet with { TargetSystem = 8 }); await f.Publish(packet with { TargetComponent = 9 });
        await f.Publish(packet with { EndPoint = new("other") }); await f.Publish(packet with { Bus = 0 });
        await f.Publish(packet with { SystemId = 2 }); await f.Publish(packet with { Id = 0x1801552A });
        await f.Publish(packet with { Id = 0xD801552A }); await f.Publish(packet);
        var received = await link.ReadAsync(Token); Assert.Equal(0x1801552Au, received.Identifier);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token); deadline.CancelAfter(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => link.ReadAsync(deadline.Token));
        await link.SendAsync(received, Token);
        var sent = Assert.IsType<CanFrameMessage>(f.WireMessages.Last()); Assert.Equal(0x9801552Au, sent.Id); Assert.Equal((byte)1, sent.Bus);
        f.State = f.State with { Flight = f.State.Flight with { IsArmed = true } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => link.SendAsync(received, Token));
        await link.DisposeAsync(); Assert.Null(f.Gate.GetCurrentOperation(new(1, 1))); Assert.Equal(0, f.BusRequests.Last());
    }
    [Fact]
    public async Task BusyVehicleDoesNotSendAForwardingStopForAnotherOwner()
    {
        var f = new Fixture(); Assert.True(f.Gate.TryAcquire(new(1, 1), "other operation", out var held));
        using (held)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => f.Link.OpenAsync(Token)); await f.Link.DisposeAsync();
            Assert.Empty(f.BusRequests); Assert.Equal("other operation", f.Gate.GetCurrentOperation(new(1, 1)));
        }
    }
    [Fact]
    public void ReadOnlyServiceRetriesCanBeReassembledWithoutAllowingReplayOfResponses()
    {
        var request = new DroneCanTransfer(24, 48, 42, 126, true, true, 0, new byte[40], DateTimeOffset.UtcNow);
        var frames = DroneCanTransferCodec.Encode(request); var parser = new DroneCanTransferCodec();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            DroneCanTransfer? result = null;
            foreach (var frame in frames) result = parser.Accept(frame, request.ObservedAt, true) ?? result;
            Assert.NotNull(result);
        }
        var response = DroneCanTransferCodec.Encode(request with { IsRequest = false, Payload = [0, 0] })[0];
        Assert.NotNull(parser.Accept(response, request.ObservedAt, true)); Assert.Null(parser.Accept(response, request.ObservedAt, true));
    }
    private sealed class Serial : IDirectSerialSession
    {
        internal readonly Channel<byte[]> Replies = Channel.CreateUnbounded<byte[]>();
        internal readonly ConcurrentQueue<string> Commands = new();
        internal bool Disposed, RejectOpen;
        public string PortName => "fake";
        public Task WriteAsync(string value, CancellationToken cancellationToken = default)
        {
            Commands.Enqueue(value); Replies.Writer.TryWrite(RejectOpen && value == "O\r" ? [7] : Encoding.ASCII.GetBytes(value.StartsWith('T') ? "Z\r" : "\r")); return Task.CompletedTask;
        }
        public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var bytes = await Replies.Reader.ReadAsync(cancellationToken); bytes.CopyTo(buffer); return bytes.Length;
        }
        public Task<string> ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
    private sealed class Fixture
    {
        internal readonly IActiveVehicleContext Active = Substitute.For<IActiveVehicleContext>();
        internal readonly EventHub Events = new(NullLogger<EventHub>.Instance);
        internal readonly VehicleOperationGate Gate = new();
        internal readonly TransportEndPoint Endpoint = new("can-test");
        internal readonly ConcurrentQueue<float> BusRequests = new();
        internal readonly ConcurrentQueue<GeneratedMavLinkMessage> WireMessages = new();
        internal VehicleState State;
        internal readonly MavLinkCanFrameLink Link;
        internal Fixture()
        {
            var now = DateTimeOffset.UtcNow;
            State = new(new(1, 1), 1, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now, VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
            Active.IsOnline.Returns(true); Active.VehicleId.Returns(new VehicleId(1, 1)); Active.State.Returns(_ => State);
            var clock = Substitute.For<IDateTimeProvider>(); clock.UtcNow.Returns(now);
            var registry = Substitute.For<IVehicleRegistry>(); registry.GetRequired(new(1, 1)).Returns(new VehicleSession(State, Endpoint, clock));
            var wire = Substitute.For<IMavLinkWireMessageEncoder>();
            wire.Encode(Arg.Any<GeneratedMavLinkMessage>()).Returns(call => { WireMessages.Enqueue(call.Arg<GeneratedMavLinkMessage>()); return new byte[] { 1 }; });
            var commands = Substitute.For<IMavLinkCommandEncoder>();
            commands.EncodeCommandLong(1, 1, 32000, Arg.Any<IReadOnlyList<float>>()).Returns(call => { BusRequests.Enqueue(call.Arg<IReadOnlyList<float>>()[0]); return new byte[] { 0 }; });
            var connection = Substitute.For<IMavLinkConnection>();
            connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>()).Returns(call => new ValueTask(RespondAsync(call.Arg<ReadOnlyMemory<byte>>().Span[0])));
            Link = new(Active, registry, Events, connection, wire, commands, Gate, new() { Bus = 2 });
        }
        private async Task RespondAsync(byte type)
        {
            if (type != 0) return;
            await Publish(new CommandAckMessage(1, 1, new("foreign"), 32000, 2, DateTimeOffset.UtcNow));
            await Publish(new CommandAckMessage(1, 1, Endpoint, 32000, 2, DateTimeOffset.UtcNow, TargetSystemId: 50));
            await Publish(new CommandAckMessage(1, 1, Endpoint, 32000, 0, DateTimeOffset.UtcNow, TargetSystemId: 255, TargetComponentId: 190));
        }
        internal Task Publish(MavLinkMessage value) => Events.PublishAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, value, Token);
    }
}
