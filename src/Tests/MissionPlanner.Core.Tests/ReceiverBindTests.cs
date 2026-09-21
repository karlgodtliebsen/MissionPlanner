using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Commands;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Exercises receiver binding through the real command safety and ACK pipeline.</summary>
public sealed class ReceiverBindTests
{
    /// <summary>FC acknowledgement maps to command status without asserting physical binding.</summary>
    [Theory]
    [InlineData(0, VehicleCommandResult.Accepted)]
    [InlineData(3, VehicleCommandResult.Unsupported)]
    [InlineData(4, VehicleCommandResult.Failed)]
    public async Task AcknowledgementsRemainCommandEvidence(byte ack, VehicleCommandResult expected)
    {
        var fixture = new Fixture();
        fixture.Protocol.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                fixture.Tracker.Handle(new CommandAckMessage(1, 1, new("test"), 500, ack, fixture.Now));
                return ValueTask.CompletedTask;
            });

        var result = await fixture.Service.StartReceiverBindAsync(new(1, 1), TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Response.Result);
        fixture.Encoder.Received(1).EncodeCommandLong(1, 1, MavLinkCommandIds.StartRxPair,
            Arg.Is<IReadOnlyList<float>>(p => p != null && p.Count == 7 && p[0] == 1 && p.Skip(1).All(v => v == 0)));
        var evidence = fixture.Telemetry.ReceivedCalls().SelectMany(c => c.GetArguments()).OfType<VehicleCommandDiagnostic>().ToArray();
        Assert.Contains(evidence, e => e.Stage == "ReceiverBindRequested" && e.ReceiverProtocol == "CRSF / ExpressLRS");
        Assert.All(evidence, e => Assert.Equal(result.CorrelationId, e.CorrelationId));
        Assert.Contains(evidence, e => e.CommandAck == ack);
        if (expected == VehicleCommandResult.Accepted)
        {
            Assert.Contains("does not confirm", result.Response.Message);
        }
    }

    /// <summary>Unknown protocols, arming, offline vehicles and foreign autopilots never transmit.</summary>
    [Theory]
    [InlineData(true, true, 512, 3)]
    [InlineData(false, false, 512, 3)]
    [InlineData(false, true, 1, 3)]
    [InlineData(false, true, 513, 3)]
    [InlineData(false, true, 512, 12)]
    public async Task UnavailableRequestsDoNotSend(bool armed, bool online, float mask, byte autopilot)
    {
        var fixture = new Fixture(armed, online, mask, autopilot);
        Assert.False(fixture.Service.GetReceiverBindAvailability(new(1, 1)).IsAvailable);
        var result = await fixture.Service.StartReceiverBindAsync(new(1, 1), TestContext.Current.CancellationToken);
        Assert.Equal(VehicleCommandResult.Denied, result.Response.Result);
        Assert.DoesNotContain(fixture.Protocol.ReceivedCalls(), call => call.GetMethodInfo().Name == "SendRawAsync");
    }

    /// <summary>Cancellation releases the ACK registration so the same command can run again.</summary>
    [Fact]
    public async Task CancellationReleasesWaiterAndOperationGate()
    {
        var fixture = new Fixture();
        using var cancel = new CancellationTokenSource();
        var command = fixture.Service.StartReceiverBindAsync(new(1, 1), cancel.Token);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command);
        fixture.Protocol.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                fixture.Tracker.Handle(new CommandAckMessage(1, 1, new("test"), 500, 0, fixture.Now));
                return ValueTask.CompletedTask;
            });
        var retry = await fixture.Service.StartReceiverBindAsync(new(1, 1), TestContext.Current.CancellationToken);
        Assert.Equal(VehicleCommandResult.Accepted, retry.Response.Result);
    }

    private sealed class Fixture
    {
        internal readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
        internal readonly IMavLinkConnection Protocol = Substitute.For<IMavLinkConnection>();
        internal readonly IMavLinkCommandEncoder Encoder = Substitute.For<IMavLinkCommandEncoder>();
        internal readonly IVehicleTelemetryEventHub Telemetry = Substitute.For<IVehicleTelemetryEventHub>();
        internal readonly CommandAckTracker Tracker = new();
        internal readonly VehicleCommandService Service;

        internal Fixture(bool armed = false, bool online = true, float mask = 512, byte autopilot = 3)
        {
            var clock = Substitute.For<IDateTimeProvider>();
            clock.UtcNow.Returns(Now);
            var registry = Substitute.For<IVehicleRegistry>();
            var state = new VehicleState(new(1, 1), 0, 2, autopilot, 0, 4, 3,
                online ? VehicleConnectionState.Online : VehicleConnectionState.Offline, Now,
                VehicleMode.Unknown, armed, null, null, null, null, null, null, null, null);
            registry.GetRequired(new(1, 1)).Returns(new VehicleSession(state, new("test"), clock));
            var parameters = new VehicleParameterRegistry();
            parameters.StoreParameter(new(1, 1), new("RC_PROTOCOLS", mask, MavParamType.Int32, 0, 1), CancellationToken.None);
            var session = Substitute.For<IVehicleConnectionSession>();
            session.Connection.Returns(Protocol);
            Service = new VehicleCommandService(registry, Substitute.For<IDomainEventHub>(), session,
                Encoder, Tracker, clock, new VehicleCommandPolicy(clock), Substitute.For<IArduPilotModeCatalog>(),
                telemetry: Telemetry, parameters: parameters);
        }
    }
}
