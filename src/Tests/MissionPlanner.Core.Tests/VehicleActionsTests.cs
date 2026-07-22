using System.Buffers.Binary;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.FlightData.Tabs;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Commands;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using NSubstitute;
using MavResult = MissionPlanner.MavLink.Generated.MavResult;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Verifies safety policy, command encoding and acknowledgement, and Actions-tab presentation behavior.
/// </summary>
public sealed class VehicleActionsTests
{
    /// <summary>Verifies common connection, heartbeat, and firmware-family safety gates.</summary>
    [Fact]
    public void PolicyRejectsUnavailableVehicleState()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = CreatePolicy(now);
        var state = CreateState(now);

        policy.Evaluate(state with { Connection = state.Connection with { State = VehicleConnectionState.Offline } }, VehicleAction.Arm).IsAllowed.Should().BeFalse();
        policy.Evaluate(state with { Connection = state.Connection with { LastHeartbeatAt = now.AddSeconds(-6) } }, VehicleAction.Arm).IsAllowed.Should().BeFalse();
        policy.Evaluate(WithFamily(state, FirmwareFamily.Unknown), VehicleAction.Arm).IsAllowed.Should().BeFalse();
    }

    /// <summary>Verifies armed, landed, and fresh-flight-state gates for basic actions.</summary>
    [Fact]
    public void PolicyRejectsInvalidArmDisarmAndInFlightStates()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = CreatePolicy(now);
        var disarmed = CreateState(now);
        var armedGround = WithFlight(disarmed, true, VehicleLandedState.OnGround, now);
        var armedAirborne = WithFlight(disarmed, true, VehicleLandedState.InAir, now);

        policy.Evaluate(armedGround, VehicleAction.Arm).IsAllowed.Should().BeFalse();
        policy.Evaluate(WithFlight(disarmed, false, VehicleLandedState.Undefined, now), VehicleAction.Arm).IsAllowed.Should().BeFalse();
        policy.Evaluate(WithFlight(disarmed, false, VehicleLandedState.OnGround, now.AddSeconds(-6)), VehicleAction.Arm).IsAllowed.Should().BeFalse();
        policy.Evaluate(disarmed, VehicleAction.Disarm).IsAllowed.Should().BeFalse();
        policy.Evaluate(armedAirborne, VehicleAction.Disarm).RequiresConfirmation.Should().BeTrue();
        policy.Evaluate(disarmed, VehicleAction.Land).IsAllowed.Should().BeFalse();
        policy.Evaluate(WithFlight(armedAirborne, true, VehicleLandedState.InAir, now.AddSeconds(-6)), VehicleAction.ReturnToLaunch).IsAllowed.Should().BeFalse();
        policy.Evaluate(armedGround, VehicleAction.Hold).IsAllowed.Should().BeFalse();
        policy.Evaluate(armedAirborne, VehicleAction.Land).IsAllowed.Should().BeTrue();
    }

    /// <summary>Verifies all takeoff, reboot, home-position, and expert confirmation gates.</summary>
    [Fact]
    public void PolicyAppliesHazardousActionGates()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = CreatePolicy(now);
        var ground = CreateState(now);
        var armedGround = WithFlight(WithFreshPosition(ground, now), true, VehicleLandedState.OnGround, now);

        policy.Evaluate(WithFamily(armedGround, FirmwareFamily.Rover), VehicleAction.Takeoff).IsAllowed.Should().BeFalse();
        policy.Evaluate(WithFlight(armedGround, false, VehicleLandedState.OnGround, now), VehicleAction.Takeoff).IsAllowed.Should().BeFalse();
        policy.Evaluate(WithFlight(armedGround, true, VehicleLandedState.InAir, now), VehicleAction.Takeoff).IsAllowed.Should().BeFalse();
        policy.Evaluate(WithFlight(armedGround, true, VehicleLandedState.OnGround, now.AddSeconds(-6)), VehicleAction.Takeoff).IsAllowed.Should().BeFalse();
        policy.Evaluate(WithFlight(ground, true, VehicleLandedState.OnGround, now), VehicleAction.Takeoff).IsAllowed.Should().BeFalse();
        policy.Evaluate(armedGround, VehicleAction.Takeoff).RequiresConfirmation.Should().BeTrue();
        policy.Evaluate(armedGround, VehicleAction.RebootAutopilot).IsAllowed.Should().BeFalse();
        policy.Evaluate(ground, VehicleAction.RebootAutopilot).RequiresConfirmation.Should().BeTrue();
        policy.Evaluate(ground, VehicleAction.SetHomeHere).IsAllowed.Should().BeFalse();
        policy.Evaluate(WithFreshPosition(ground, now), VehicleAction.SetHomeHere).RequiresConfirmation.Should().BeTrue();
        policy.Evaluate(ground, VehicleAction.ExpertCommand).RequiresConfirmation.Should().BeTrue();
    }

    /// <summary>Verifies that firmware families receive different ArduPilot custom-mode catalogs.</summary>
    [Fact]
    public void ModeCatalogUsesFirmwareSpecificCustomModes()
    {
        var catalog = new ArduPilotModeCatalog();

        catalog.Find(FirmwareFamily.ArduCopter, VehicleMode.Rtl)!.CustomMode.Should().Be(6);
        catalog.Find(FirmwareFamily.ArduPlane, VehicleMode.Rtl)!.CustomMode.Should().Be(11);
        catalog.Find(FirmwareFamily.Rover, VehicleMode.Loiter)!.Name.Should().Be("Hold");
        catalog.Find(FirmwareFamily.ArduSub, VehicleMode.Rtl)!.Name.Should().Be("Surface");
        catalog.GetModes(FirmwareFamily.Unknown).Should().BeEmpty();
    }

    /// <summary>Verifies the COMMAND_LONG wire fields used by every supported standard action.</summary>
    /// <param name="action">The human-readable action.</param>
    /// <param name="commandId">The expected MAV_CMD identifier.</param>
    [Theory]
    [InlineData("Arm", MavLinkCommandIds.ComponentArmDisarm)]
    [InlineData("Disarm", MavLinkCommandIds.ComponentArmDisarm)]
    [InlineData("Set Mode", MavLinkCommandIds.DoSetMode)]
    [InlineData("Takeoff", MavLinkCommandIds.NavTakeoff)]
    [InlineData("Land", MavLinkCommandIds.DoSetMode)]
    [InlineData("RTL", MavLinkCommandIds.DoSetMode)]
    [InlineData("Hold", MavLinkCommandIds.DoSetMode)]
    [InlineData("Reboot", MavLinkCommandIds.PreflightRebootShutdown)]
    [InlineData("Set Home", MavLinkCommandIds.DoSetHome)]
    [InlineData("Expert", 300)]
    public void EncoderTargetsExpectedCommand(string action, ushort commandId)
    {
        var crc = Substitute.For<IMavLinkCrcExtraProvider>();
        crc.TryGetCrcExtra(MessageIds.CommandLong, out Arg.Any<byte>()).Returns(call =>
        {
            call[1] = (byte)152;
            return true;
        });
        var encoder = new MavLinkCommandEncoder(crc);

        var packet = encoder.EncodeCommandLong(12, 34, commandId, [1, 2, 3, 4, 5, 6, 7]);

        action.Should().NotBeNullOrWhiteSpace();
        BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(38, 2)).Should().Be(commandId);
        packet[40].Should().Be(12);
        packet[41].Should().Be(34);
        BitConverter.ToSingle(packet, 10 + (6 * 4)).Should().Be(7);
    }

    /// <summary>Verifies that each typed action is sent with its expected command ID and reports its ACK.</summary>
    [Fact]
    public async Task ServiceRoutesEveryTypedActionThroughAcknowledgement()
    {
        var now = DateTimeOffset.UtcNow;
        var ground = WithFreshPosition(CreateState(now), now);
        var armedGround = WithFlight(ground, true, VehicleLandedState.OnGround, now);
        var airborne = WithFlight(ground, true, VehicleLandedState.InAir, now);

        await AssertAcknowledgedAsync(ground, MavLinkCommandIds.ComponentArmDisarm, (service, id) => service.ArmAsync(id, TestContext.Current.CancellationToken));
        await AssertAcknowledgedAsync(armedGround, MavLinkCommandIds.ComponentArmDisarm, (service, id) => service.DisarmAsync(id, false, TestContext.Current.CancellationToken));
        await AssertAcknowledgedAsync(ground, MavLinkCommandIds.DoSetMode, (service, id) => service.SetModeAsync(id, new ArduPilotModeCatalog().GetModes(FirmwareFamily.ArduCopter)[0], TestContext.Current.CancellationToken));
        await AssertAcknowledgedAsync(armedGround, MavLinkCommandIds.NavTakeoff, (service, id) => service.TakeoffAsync(id, 12, true, TestContext.Current.CancellationToken));
        await AssertAcknowledgedAsync(airborne, MavLinkCommandIds.DoSetMode, (service, id) => service.LandAsync(id, TestContext.Current.CancellationToken));
        await AssertAcknowledgedAsync(airborne, MavLinkCommandIds.DoSetMode, (service, id) => service.ReturnToLaunchAsync(id, TestContext.Current.CancellationToken));
        await AssertAcknowledgedAsync(airborne, MavLinkCommandIds.DoSetMode, (service, id) => service.HoldAsync(id, TestContext.Current.CancellationToken));
        await AssertAcknowledgedAsync(ground, MavLinkCommandIds.PreflightRebootShutdown, (service, id) => service.RebootAutopilotAsync(id, true, TestContext.Current.CancellationToken));
        await AssertAcknowledgedAsync(ground, MavLinkCommandIds.DoSetHome, (service, id) => service.SetHomeHereAsync(id, true, TestContext.Current.CancellationToken));
        await AssertAcknowledgedAsync(ground, 300, (service, id) => service.ExecuteExpertAsync(new ExpertVehicleCommand(id, 300, [0, 0, 0, 0, 0, 0, 0]), true, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies that a progress ACK does not complete a command before its terminal ACK arrives.</summary>
    [Fact]
    public async Task AckTrackerWaitsForTerminalResult()
    {
        var now = DateTimeOffset.UtcNow;
        var vehicleId = new VehicleId(1, 1);
        var tracker = new CommandAckTracker();
        var pending = tracker.WaitForAckAsync(vehicleId, 400, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        tracker.Handle(new CommandAckMessage(1, 1, new TransportEndPoint("test"), 400, (byte)MavResult.InProgress, now));
        pending.IsCompleted.Should().BeFalse();
        tracker.Handle(new CommandAckMessage(1, 1, new TransportEndPoint("test"), 400, (byte)MavResult.Accepted, now));

        (await pending).Result.Should().Be((byte)MavResult.Accepted);
    }

    /// <summary>Verifies that only one command can be pending for a vehicle at a time.</summary>
    [Fact]
    public async Task ServiceRejectsDuplicateConcurrentVehicleCommand()
    {
        var now = DateTimeOffset.UtcNow;
        var state = CreateState(now);
        var registry = Substitute.For<IVehicleRegistry>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        registry.GetRequired(state.VehicleId).Returns(new VehicleSession(state, new TransportEndPoint("test"), clock));
        var encoder = Substitute.For<IMavLinkCommandEncoder>();
        encoder.EncodeCommandLong(Arg.Any<byte>(), Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<IReadOnlyList<float>>()).Returns([1]);
        var ackCompletion = new TaskCompletionSource<CommandAckMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tracker = Substitute.For<ICommandAckTracker>();
        tracker.WaitForAckAsync(Arg.Any<VehicleId>(), Arg.Any<ushort>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                waitStarted.TrySetResult();
                return ackCompletion.Task;
            });
        var service = new VehicleCommandService(
            registry,
            Substitute.For<IDomainEventHub>(),
            Substitute.For<IMavLinkConnection>(),
            encoder,
            tracker,
            clock,
            new VehicleCommandPolicy(clock),
            new ArduPilotModeCatalog());

        var first = service.ArmAsync(state.VehicleId, TestContext.Current.CancellationToken);
        await waitStarted.Task;
        var duplicate = await service.ArmAsync(state.VehicleId, TestContext.Current.CancellationToken);
        ackCompletion.SetResult(new CommandAckMessage(1, 1, new TransportEndPoint("test"), MavLinkCommandIds.ComponentArmDisarm, 0, now));

        duplicate.Result.Should().Be(VehicleCommandResult.Busy);
        (await first).Result.Should().Be(VehicleCommandResult.Accepted);
    }

    /// <summary>Verifies that declining an airborne-disarm confirmation prevents transmission.</summary>
    [Fact]
    public async Task ViewModelRequiresConfirmationBeforeHazardousCommand()
    {
        var now = DateTimeOffset.UtcNow;
        var state = WithFlight(CreateState(now), true, VehicleLandedState.InAir, now);
        var fixture = CreateViewModel(state, now);
        fixture.Confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await fixture.ViewModel.DisarmCommand.ExecuteAsync(null);

        await fixture.Commands.DidNotReceive().DisarmAsync(
            Arg.Any<VehicleId>(), Arg.Any<bool>(), TestContext.Current.CancellationToken);
        fixture.ViewModel.OperationState.Status.Should().Be(AsyncOperationStatus.Warning);
    }

    /// <summary>Verifies that cancellation while confirming produces a stable cancelled presentation state.</summary>
    [Fact]
    public async Task ViewModelHandlesConfirmationCancellation()
    {
        var now = DateTimeOffset.UtcNow;
        var fixture = CreateViewModel(CreateState(now), now);
        fixture.Confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new OperationCanceledException());

        await fixture.ViewModel.RebootCommand.ExecuteAsync(null);

        fixture.ViewModel.OperationState.Status.Should().Be(AsyncOperationStatus.Warning);
        await fixture.Commands.DidNotReceive().RebootAutopilotAsync(
            Arg.Any<VehicleId>(), Arg.Any<bool>(), TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies timeout and rejected ACK responses are presented distinctly.</summary>
    [Fact]
    public async Task ViewModelPresentsTimeoutAndRejectedAcknowledgements()
    {
        var now = DateTimeOffset.UtcNow;
        var timeout = CreateViewModel(CreateState(now), now);
        timeout.Commands.ArmAsync(Arg.Any<VehicleId>(), Arg.Any<CancellationToken>())
            .Returns(new VehicleCommandResponse(timeout.State.VehicleId, VehicleCommandResult.Timeout, now, "ACK timed out."));
        await timeout.ViewModel.ArmCommand.ExecuteAsync(null);
        timeout.ViewModel.OperationState.Status.Should().Be(AsyncOperationStatus.Timeout);

        var rejected = CreateViewModel(CreateState(now), now);
        rejected.Commands.ArmAsync(Arg.Any<VehicleId>(), Arg.Any<CancellationToken>())
            .Returns(new VehicleCommandResponse(rejected.State.VehicleId, VehicleCommandResult.TemporarilyRejected, now, "Try later."));
        await rejected.ViewModel.ArmCommand.ExecuteAsync(null);
        rejected.ViewModel.OperationState.Status.Should().Be(AsyncOperationStatus.Warning);
    }

    /// <summary>Verifies an in-progress action is cancelled and marked disconnected with its connection lifetime.</summary>
    [Fact]
    public async Task ViewModelCancelsPendingCommandOnDisconnect()
    {
        var now = DateTimeOffset.UtcNow;
        var state = CreateState(now);
        using var connectionLifetime = new CancellationTokenSource();
        var active = Substitute.For<IActiveVehicleContext>();
        var online = true;
        active.Current.Returns(new ActiveVehicleSnapshot(state.VehicleId, state));
        active.VehicleId.Returns(state.VehicleId);
        active.State.Returns(state);
        active.IsOnline.Returns(_ => online);
        active.ConnectionCancellationToken.Returns(connectionLifetime.Token);
        var commands = Substitute.For<IVehicleCommandService>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        commands.ArmAsync(state.VehicleId, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, call.ArgAt<CancellationToken>(1));
            return new VehicleCommandResponse(state.VehicleId, VehicleCommandResult.Accepted, now);
        });
        var viewModel = CreateViewModel(active, commands, now).ViewModel;

        var operation = viewModel.ArmCommand.ExecuteAsync(null);
        await started.Task;
        online = false;
        connectionLifetime.Cancel();
        await operation;

        viewModel.OperationState.Status.Should().Be(AsyncOperationStatus.Disconnected);
        viewModel.PendingCommand.Should().BeNull();
    }

    private static async Task AssertAcknowledgedAsync(
        VehicleState state,
        ushort expectedCommand,
        Func<IVehicleCommandService, VehicleId, Task<VehicleCommandResponse>> execute)
    {
        var now = state.LastHeartbeatAt;
        var registry = Substitute.For<IVehicleRegistry>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        registry.GetRequired(state.VehicleId).Returns(new VehicleSession(state, new TransportEndPoint("test"), clock));
        var encoder = Substitute.For<IMavLinkCommandEncoder>();
        encoder.EncodeCommandLong(Arg.Any<byte>(), Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<IReadOnlyList<float>>()).Returns([1, 2, 3]);
        var tracker = Substitute.For<ICommandAckTracker>();
        tracker.WaitForAckAsync(state.VehicleId, expectedCommand, Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new CommandAckMessage(state.VehicleId.SystemId, state.VehicleId.ComponentId, new TransportEndPoint("test"), expectedCommand, 0, now));
        var connection = Substitute.For<IMavLinkConnection>();
        var service = new VehicleCommandService(
            registry,
            Substitute.For<IDomainEventHub>(),
            connection,
            encoder,
            tracker,
            clock,
            new VehicleCommandPolicy(clock),
            new ArduPilotModeCatalog());

        var response = await execute(service, state.VehicleId);

        response.Result.Should().Be(VehicleCommandResult.Accepted);
        encoder.Received(1).EncodeCommandLong(state.VehicleId.SystemId, state.VehicleId.ComponentId, expectedCommand, Arg.Any<IReadOnlyList<float>>());
        await connection.Received(1).SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>());
    }

    private static ViewModelFixture CreateViewModel(VehicleState state, DateTimeOffset now)
    {
        var active = Substitute.For<IActiveVehicleContext>();
        active.Current.Returns(new ActiveVehicleSnapshot(state.VehicleId, state));
        active.VehicleId.Returns(state.VehicleId);
        active.State.Returns(state);
        active.IsOnline.Returns(true);
        active.ConnectionCancellationToken.Returns(CancellationToken.None);
        return CreateViewModel(active, Substitute.For<IVehicleCommandService>(), now);
    }

    private static ViewModelFixture CreateViewModel(IActiveVehicleContext active, IVehicleCommandService commands, DateTimeOffset now)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var confirmation = Substitute.For<IUserConfirmationService>();
        var dispatcher = Substitute.For<IDispatcher>();
        dispatcher.Dispatch(Arg.Any<Action>()).Returns(call =>
        {
            call.Arg<Action>()!.Invoke();
            return true;
        });
        var viewModel = new ActionsTabViewModel(
            active,
            commands,
            new VehicleCommandPolicy(clock),
            new ArduPilotModeCatalog(),
            confirmation,
            dispatcher,
            Substitute.For<ILogger<ActionsTabViewModel>>());
        return new ViewModelFixture(viewModel, commands, confirmation, active.State!);
    }

    private static VehicleCommandPolicy CreatePolicy(DateTimeOffset now)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return new VehicleCommandPolicy(clock);
    }

    private static VehicleState CreateState(DateTimeOffset now) =>
        new VehicleState(new VehicleId(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now, VehicleMode.Stabilize,
            false, null, null, null, null, null, null, null, null) with
        {
            Flight = new VehicleFlightState(0, 0, 4, VehicleMode.Stabilize, false, LandedState: VehicleLandedState.OnGround, ObservedAt: now)
        };

    private static VehicleState WithFlight(VehicleState state, bool armed, VehicleLandedState landedState, DateTimeOffset observedAt) =>
        state with { Flight = state.Flight with { IsArmed = armed, LandedState = landedState, ObservedAt = observedAt } };

    private static VehicleState WithFreshPosition(VehicleState state, DateTimeOffset now) => state with
    {
        Gps = VehicleGpsState.Empty with { FixType = GpsFixType.Fix3D, ObservedAt = now },
        Position = VehiclePositionState.Empty with { LatitudeDegrees = 55, LongitudeDegrees = 12, ObservedAt = now }
    };

    private static VehicleState WithFamily(VehicleState state, FirmwareFamily family) => state with
    {
        Identity = state.Identity with { Firmware = state.Identity.Firmware with { Family = family } }
    };

    private sealed record ViewModelFixture(
        ActionsTabViewModel ViewModel,
        IVehicleCommandService Commands,
        IUserConfirmationService Confirmation,
        VehicleState State);
}
