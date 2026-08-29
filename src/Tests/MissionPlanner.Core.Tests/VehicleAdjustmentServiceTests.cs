using FluentAssertions;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.FlightData.Adjustments;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class VehicleAdjustmentServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly TransportEndPoint EndPoint = new("test");

    [Theory]
    [InlineData(FirmwareFamily.ArduCopter, VehicleSpeedTargetType.GroundSpeed, 1)]
    [InlineData(FirmwareFamily.Rover, VehicleSpeedTargetType.GroundSpeed, 1)]
    [InlineData(FirmwareFamily.ArduPlane, VehicleSpeedTargetType.Airspeed, 0)]
    [InlineData(FirmwareFamily.ArduPlane, VehicleSpeedTargetType.GroundSpeed, 1)]
    public async Task SpeedUsesSemanticTypeAndNoThrottleChange(FirmwareFamily family, VehicleSpeedTargetType type, float expectedType)
    {
        var fixture = CreateFixture(State(family, family == FirmwareFamily.ArduCopter ? 4u : 15u));
        fixture.Connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), EndPoint, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            fixture.Acks.Handle(new CommandAckMessage(1, 1, EndPoint, (ushort)MavCmd.DoChangeSpeed, 0, Now));
            return ValueTask.CompletedTask;
        });

        var result = await fixture.Service.ChangeSpeedAsync(fixture.VehicleId, type, 12.5, TestContext.Current.CancellationToken);

        result.Status.Should().Be(VehicleAdjustmentStatus.CommandAccepted);
        fixture.CommandEncoder.Received(1).EncodeCommandLong(1, 1, (ushort)MavCmd.DoChangeSpeed,
            Arg.Is<IReadOnlyList<float>>(values => values[0] == expectedType && values[1] == 12.5f && values[2] == -1));
    }

    [Fact]
    public async Task AirspeedForCopterAndInvalidSpeedAreDeniedBeforeSend()
    {
        var fixture = CreateFixture(State(FirmwareFamily.ArduCopter, 4));
        (await fixture.Service.ChangeSpeedAsync(fixture.VehicleId, VehicleSpeedTargetType.Airspeed, 10, TestContext.Current.CancellationToken)).Status.Should().Be(VehicleAdjustmentStatus.Denied);
        (await fixture.Service.ChangeSpeedAsync(fixture.VehicleId, VehicleSpeedTargetType.GroundSpeed, double.NaN, TestContext.Current.CancellationToken)).Status.Should().Be(VehicleAdjustmentStatus.Denied);
        await fixture.Connection.DidNotReceiveWithAnyArgs().SendRawAsync(default, default!, default);
    }

    [Theory]
    [InlineData(FirmwareFamily.ArduCopter, 4u)]
    [InlineData(FirmwareFamily.ArduPlane, 15u)]
    public async Task GuidedAltitudeUsesHomeRelativeGlobalPositionTarget(FirmwareFamily family, uint mode)
    {
        var state = State(family, mode) with
        {
            Position = VehiclePositionState.Empty with { LatitudeDegrees = 55.5, LongitudeDegrees = 12.25, RelativeAltitudeMeters = 10, ObservedAt = Now }
        };
        var fixture = CreateFixture(state);
        fixture.Connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), EndPoint, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            fixture.PublishState(state with { Position = state.Position with { RelativeAltitudeMeters = 25.2 } }).GetAwaiter().GetResult();
            return ValueTask.CompletedTask;
        });

        var result = await fixture.Service.SetGuidedAltitudeAsync(fixture.VehicleId, 25, TestContext.Current.CancellationToken);

        result.Status.Should().Be(VehicleAdjustmentStatus.TelemetryConfirmed);
        var encoded = Assert.IsType<SetPositionTargetGlobalIntMessage>(fixture.WireEncoder.ReceivedCalls().Single().GetArguments()[0]);
        encoded.CoordinateFrame.Should().Be((byte)MavFrame.GlobalRelativeAltInt);
        encoded.LatInt.Should().Be(555_000_000);
        encoded.LonInt.Should().Be(122_500_000);
        encoded.Alt.Should().Be(25);
        encoded.TypeMask.Should().Be((ushort)(PositionTargetTypemask.VxIgnore | PositionTargetTypemask.VyIgnore | PositionTargetTypemask.VzIgnore |
            PositionTargetTypemask.AxIgnore | PositionTargetTypemask.AyIgnore | PositionTargetTypemask.AzIgnore |
            PositionTargetTypemask.YawIgnore | PositionTargetTypemask.YawRateIgnore));
    }

    [Fact]
    public async Task LoiterRadiusPrefersWpParameterAndPreservesNegativeDirection()
    {
        var parameterRegistry = new VehicleParameterRegistry();
        var state = State(FirmwareFamily.ArduPlane, 12);
        parameterRegistry.StoreParameter(state.VehicleId, new VehicleParameter("LOITER_RAD", 30, MissionPlanner.MavLink.Parameters.MavParamType.Real32, 0, 2), TestContext.Current.CancellationToken);
        parameterRegistry.StoreParameter(state.VehicleId, new VehicleParameter("WP_LOITER_RAD", -40, MissionPlanner.MavLink.Parameters.MavParamType.Real32, 1, 2), TestContext.Current.CancellationToken);
        var parameterService = Substitute.For<IVehicleParameterService>();
        parameterService.SetParameterAsync(state.VehicleId, Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MissionPlanner.MavLink.Parameters.MavParamType>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            parameterRegistry.StoreParameter(state.VehicleId,
                new VehicleParameter(call.ArgAt<string>(1), call.ArgAt<float>(2), call.ArgAt<MissionPlanner.MavLink.Parameters.MavParamType>(3), 1, 2), call.ArgAt<CancellationToken>(4));
            return true;
        });
        var fixture = CreateFixture(state, parameterRegistry, parameterService);

        var result = await fixture.Service.SetLoiterRadiusAsync(state.VehicleId, 75, TestContext.Current.CancellationToken);

        result.Status.Should().Be(VehicleAdjustmentStatus.ParameterConfirmed);
        result.PersistedValue.Should().Be(-75);
        await parameterService.Received(1).SetParameterAsync(state.VehicleId, "WP_LOITER_RAD", -75, MissionPlanner.MavLink.Parameters.MavParamType.Real32, Arg.Any<CancellationToken>());
    }

    private static Fixture CreateFixture(VehicleState state, IVehicleParameterRegistry? parameterRegistry = null, IVehicleParameterService? parameterService = null)
    {
        var vehicleRegistry = Substitute.For<IVehicleRegistry>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        vehicleRegistry.GetRequired(state.VehicleId).Returns(new VehicleSession(state, EndPoint, clock));
        var connection = Substitute.For<IMavLinkConnection>();
        var connectionSession = Substitute.For<IVehicleConnectionSession>();
        connectionSession.Connection.Returns(connection);
        var commandEncoder = Substitute.For<IMavLinkCommandEncoder>();
        commandEncoder.EncodeCommandLong(Arg.Any<byte>(), Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<IReadOnlyList<float>>()).Returns([1]);
        var wireEncoder = Substitute.For<IMavLinkWireMessageEncoder>();
        wireEncoder.Encode(Arg.Any<GeneratedMavLinkMessage>()).Returns([2]);
        var acks = new CommandAckTracker();
        var domainHub = Substitute.For<IDomainEventHub>();
        Func<VehicleStateUpdated, CancellationToken, Task>? publishState = null;
        domainHub.SubscribeDomainEventAsync(Arg.Any<Func<VehicleStateUpdated, CancellationToken, Task>>()).Returns(call =>
        {
            publishState = call.Arg<Func<VehicleStateUpdated, CancellationToken, Task>>();
            return Substitute.For<IDisposable>();
        });
        parameterRegistry ??= Substitute.For<IVehicleParameterRegistry>();
        parameterService ??= Substitute.For<IVehicleParameterService>();
        var service = new VehicleAdjustmentService(vehicleRegistry, connectionSession, commandEncoder, wireEncoder, acks,
            new VehicleOperationGate(), domainHub, parameterRegistry, parameterService);
        return new Fixture(state.VehicleId, service, connection, commandEncoder, wireEncoder, acks,
            next => publishState!(new VehicleStateUpdated(next), TestContext.Current.CancellationToken));
    }

    private static VehicleState State(FirmwareFamily family, uint mode)
    {
        var state = new VehicleState(new VehicleId(1, 1), mode, 2, 3, 0, 4, 3, VehicleConnectionState.Online, Now, VehicleMode.Unknown, true, null, null, null, null, null, null, null, null);
        return state with { Identity = state.Identity with { Firmware = state.Identity.Firmware with { Family = family } } };
    }

    private sealed record Fixture(VehicleId VehicleId, VehicleAdjustmentService Service, IMavLinkConnection Connection,
        IMavLinkCommandEncoder CommandEncoder, IMavLinkWireMessageEncoder WireEncoder, CommandAckTracker Acks, Func<VehicleState, Task> PublishState);
}
