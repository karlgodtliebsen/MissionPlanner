using FluentAssertions;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Missions;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Missions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;
using MavCmd = MissionPlanner.MavLink.Generated.MavCmd;
using MavResult = MissionPlanner.MavLink.Generated.MavResult;
using MissionState = MissionPlanner.MavLink.Generated.MissionState;

namespace MissionPlanner.Core.Tests;

/// <summary>Validates typed mission intervention commands and confirmation semantics.</summary>
public sealed class MissionInterventionServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;
    private static readonly TransportEndPoint EndPoint = new("test");

    [Fact]
    public async Task SetCurrentUsesModernCommandAndPostRequestTelemetry()
    {
        var fixture = CreateFixture(State(missionCount: 5));
        fixture.Connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), EndPoint, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            fixture.Acks.Handle(new CommandAckMessage(1, 1, EndPoint, (ushort)MavCmd.DoSetMissionCurrent, 0, Now));
            fixture.PublishMissionCurrent(3).GetAwaiter().GetResult();
            return ValueTask.CompletedTask;
        });

        var result = await fixture.Service.SetCurrentMissionItemAsync(fixture.VehicleId, 3, TestContext.Current.CancellationToken);

        result.Status.Should().Be(MissionInterventionStatus.TelemetryConfirmed);
        fixture.CommandEncoder.Received(1).EncodeCommandLong(1, 1, (ushort)MavCmd.DoSetMissionCurrent,
            Arg.Is<IReadOnlyList<float>>(values => values[0] == 3 && values[1] == 0));
    }

    [Fact]
    public async Task UnsupportedSetCurrentUsesOneLegacyFallbackButRestartDoesNot()
    {
        var fixture = CreateFixture(State(missionCount: 5));
        var sends = 0;
        fixture.Connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), EndPoint, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            sends++;
            if (sends == 1)
            {
                fixture.Acks.Handle(new CommandAckMessage(1, 1, EndPoint, (ushort)MavCmd.DoSetMissionCurrent, (byte)MavResult.Unsupported, Now));
            }
            else
            {
                fixture.PublishMissionCurrent(2).GetAwaiter().GetResult();
            }
            return ValueTask.CompletedTask;
        });

        var fallback = await fixture.Service.SetCurrentMissionItemAsync(fixture.VehicleId, 2, TestContext.Current.CancellationToken);
        fallback.Status.Should().Be(MissionInterventionStatus.FallbackTelemetryConfirmed);
        fixture.MissionEncoder.Received(1).EncodeMissionSetCurrent(1, 1, 2);

        var restartFixture = CreateFixture(State(missionCount: 5));
        restartFixture.Connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), EndPoint, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            restartFixture.Acks.Handle(new CommandAckMessage(1, 1, EndPoint, (ushort)MavCmd.DoSetMissionCurrent, (byte)MavResult.Unsupported, Now));
            return ValueTask.CompletedTask;
        });
        var restart = await restartFixture.Service.RestartMissionAsync(restartFixture.VehicleId, TestContext.Current.CancellationToken);
        restart.Status.Should().Be(MissionInterventionStatus.Unsupported);
        restartFixture.CommandEncoder.Received(1).EncodeCommandLong(1, 1, (ushort)MavCmd.DoSetMissionCurrent,
            Arg.Is<IReadOnlyList<float>>(values => values[0] == 0 && values[1] == 1));
        restartFixture.MissionEncoder.DidNotReceiveWithAnyArgs().EncodeMissionSetCurrent(default, default, default);
    }

    [Fact]
    public async Task ResumeRequiresPausedEvidenceAndSendsOnlyPauseContinue()
    {
        var paused = State(missionCount: 5) with
        {
            Navigation = State(missionCount: 5).Navigation with
            {
                MissionState = MissionState.Paused,
                MissionMode = VehicleMissionMode.Suspended
            }
        };
        var fixture = CreateFixture(paused);
        fixture.Connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), EndPoint, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            fixture.Acks.Handle(new CommandAckMessage(1, 1, EndPoint, (ushort)MavCmd.DoPauseContinue, 0, Now));
            fixture.PublishMissionCurrent(2, MissionState.Active, VehicleMissionMode.Mission).GetAwaiter().GetResult();
            return ValueTask.CompletedTask;
        });

        var result = await fixture.Service.ResumeMissionAsync(fixture.VehicleId, TestContext.Current.CancellationToken);
        result.Status.Should().Be(MissionInterventionStatus.TelemetryConfirmed);
        fixture.CommandEncoder.Received(1).EncodeCommandLong(1, 1, (ushort)MavCmd.DoPauseContinue,
            Arg.Is<IReadOnlyList<float>>(values => values[0] == 1));

        var unknown = CreateFixture(State(missionCount: 5));
        (await unknown.Service.ResumeMissionAsync(unknown.VehicleId, TestContext.Current.CancellationToken)).Status.Should().Be(MissionInterventionStatus.Denied);
        await unknown.Connection.DidNotReceiveWithAnyArgs().SendRawAsync(default, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AbortLandingRequiresVerifiedPlaneAutoLandAndEnabledParameter()
    {
        var state = State(missionCount: 5, family: FirmwareFamily.ArduPlane, customMode: 10) with
        {
            Navigation = State(missionCount: 5).Navigation with
            {
                CurrentMissionSequence = 4,
                MissionState = MissionState.Active,
                MissionMode = VehicleMissionMode.Mission,
                MissionId = 77
            }
        };
        var domainHub = Substitute.For<IDomainEventHub>();
        var snapshots = new OnboardMissionSnapshotStore(domainHub);
        snapshots.Record(new OnboardMissionSnapshot(state.VehicleId, MissionPlanType.FlightMission,
            [new MavLinkMissionItem(4, 0, (ushort)MavCmd.NavLand, false, true, 0, 0, 0, 0, 0, 0, 0, MissionPlanner.MavLink.Missions.MavMissionType.Mission)], 77, Now));
        var parameterRegistry = new VehicleParameterRegistry();
        parameterRegistry.StoreParameter(state.VehicleId, new VehicleParameter("LAND_ABORT_THR", 1, MavParamType.Real32, 0, 1), TestContext.Current.CancellationToken);
        var fixture = CreateFixture(state, snapshots, parameterRegistry);
        fixture.Connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), EndPoint, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            fixture.Acks.Handle(new CommandAckMessage(1, 1, EndPoint, (ushort)MavCmd.DoGoAround, 0, Now));
            return ValueTask.CompletedTask;
        });

        var result = await fixture.Service.AbortLandingAsync(fixture.VehicleId, TestContext.Current.CancellationToken);
        result.Status.Should().Be(MissionInterventionStatus.AcceptedButNotTelemetryConfirmed);
        fixture.CommandEncoder.Received(1).EncodeCommandLong(1, 1, (ushort)MavCmd.DoGoAround, Arg.Any<IReadOnlyList<float>>());

        parameterRegistry.ClearParameters(state.VehicleId);
        (await fixture.Service.AbortLandingAsync(fixture.VehicleId, TestContext.Current.CancellationToken)).Status.Should().Be(MissionInterventionStatus.Denied);
    }

    private static Fixture CreateFixture(VehicleState state, IOnboardMissionSnapshotStore? snapshots = null, IVehicleParameterRegistry? parameters = null)
    {
        var registry = Substitute.For<IVehicleRegistry>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var session = new VehicleSession(state, EndPoint, clock);
        registry.GetRequired(state.VehicleId).Returns(session);
        var connection = Substitute.For<IMavLinkConnection>();
        var connectionSession = Substitute.For<IVehicleConnectionSession>();
        connectionSession.Connection.Returns(connection);
        var commandEncoder = Substitute.For<IMavLinkCommandEncoder>();
        commandEncoder.EncodeCommandLong(Arg.Any<byte>(), Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<IReadOnlyList<float>>()).Returns([1]);
        var missionEncoder = Substitute.For<IMavLinkMissionEncoder>();
        missionEncoder.EncodeMissionSetCurrent(Arg.Any<byte>(), Arg.Any<byte>(), Arg.Any<ushort>()).Returns([2]);
        var acks = new CommandAckTracker();
        var eventHub = Substitute.For<IEventHub>();
        Func<MavLinkMessage, CancellationToken, Task>? publish = null;
        eventHub.SubscribeAsync(MavLinkEventTopics.ReceivedMessage, Arg.Any<Func<MavLinkMessage, CancellationToken, Task>>()).Returns(call =>
        {
            publish = call.Arg<Func<MavLinkMessage, CancellationToken, Task>>();
            return Substitute.For<IDisposable>();
        });
        snapshots ??= Substitute.For<IOnboardMissionSnapshotStore>();
        parameters ??= Substitute.For<IVehicleParameterRegistry>();
        var service = new MissionInterventionService(registry, connectionSession, commandEncoder, missionEncoder, acks,
            new VehicleOperationGate(), eventHub, snapshots, parameters);
        return new Fixture(state.VehicleId, service, connection, commandEncoder, missionEncoder, acks,
            (sequence, missionState, missionMode) => publish!(new MissionCurrentMessage(1, 1, EndPoint, sequence, 5, (byte)missionState, (byte)missionMode, Now, 77), TestContext.Current.CancellationToken));
    }

    private static VehicleState State(ushort? missionCount, FirmwareFamily family = FirmwareFamily.ArduCopter, uint customMode = 3)
    {
        var state = new VehicleState(new VehicleId(1, 1), customMode, 2, 3, 0, 4, 3, VehicleConnectionState.Online, Now, VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
        return state with
        {
            Identity = state.Identity with
            {
                Firmware = state.Identity.Firmware with
                {
                    Family = family
                }
            },
            Navigation = state.Navigation with
            {
                MissionItemCount = missionCount,
                CurrentMissionSequence = 2
            }
        };
    }

    private sealed record Fixture(
        VehicleId VehicleId,
        MissionInterventionService Service,
        IMavLinkConnection Connection,
        IMavLinkCommandEncoder CommandEncoder,
        IMavLinkMissionEncoder MissionEncoder,
        CommandAckTracker Acks,
        Func<ushort, MissionState, VehicleMissionMode, Task> Publish)
    {
        public Task PublishMissionCurrent(ushort sequence, MissionState state = MissionState.Active, VehicleMissionMode mode = VehicleMissionMode.Mission)
        {
            return Publish(sequence, state, mode);
        }
    }
}
