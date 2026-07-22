using System.Buffers.Binary;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.InitSetup;
using MissionPlanner.App.Views.InitSetup.Tabs;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub;
using MissionPlanner.MavLink;
using MissionPlanner.MavLink.Decoding;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies the ArduPilot accelerometer and level calibration workflows.</summary>
public sealed class AccelerometerCalibrationTests
{
    private static readonly VehicleId vehicleId = new(1, 1);
    private static readonly TransportEndPoint endPoint = new("test");

    /// <summary>Verifies all six requested positions, supplemental text, duplicates, and explicit success.</summary>
    [Fact]
    public async Task SixPositionWorkflowFollowsExplicitOrientationProtocol()
    {
        using var fixture = CreateFixture();
        var start = fixture.Service.StartSixPositionAsync(vehicleId, TestContext.Current.CancellationToken);
        await fixture.CommandSent.Task;
        await fixture.PublishOrientationAsync(CalibrationOrientation.Level);
        await start;

        fixture.Service.Current.State.Should().Be(CalibrationWorkflowState.WaitingForOrientation);
        fixture.Service.Current.RequiredOrientation.Should().Be(CalibrationOrientation.Level);
        fixture.MessageStore.Add(new VehicleStatusText(vehicleId, 1, 1, MavLink.MavSeverity.Info,
            "Place vehicle level", DateTimeOffset.UtcNow));
        fixture.Service.Current.SupplementalStatus.Should().Be("Place vehicle level");

        await fixture.PublishOrientationAsync(CalibrationOrientation.Left);
        fixture.Service.Current.RequiredOrientation.Should().Be(CalibrationOrientation.Level, "out-of-order prompts are ignored before confirmation");
        await fixture.Service.ConfirmOrientationAsync(TestContext.Current.CancellationToken);
        await fixture.PublishOrientationAsync(CalibrationOrientation.Left);
        fixture.Service.Current.CompletedOrientations.Should().Contain(CalibrationOrientation.Level);
        await fixture.PublishOrientationAsync(CalibrationOrientation.Left);
        fixture.Service.Current.CompletedOrientations.Should().HaveCount(1, "duplicate prompts do not advance progress");

        await AdvanceAsync(fixture, CalibrationOrientation.Right);
        await AdvanceAsync(fixture, CalibrationOrientation.NoseDown);
        await AdvanceAsync(fixture, CalibrationOrientation.NoseUp);
        await AdvanceAsync(fixture, CalibrationOrientation.Back);
        await fixture.Service.ConfirmOrientationAsync(TestContext.Current.CancellationToken);
        await fixture.PublishRawOrientationAsync((float)AccelcalVehiclePos.Success);

        fixture.Service.Current.State.Should().Be(CalibrationWorkflowState.Success);
        fixture.Service.Current.CompletedOrientations.Should().HaveCount(6);
        fixture.Service.Current.Progress.Should().Be(1);
        fixture.Gate.TryAcquire(vehicleId, "after calibration", out var lease).Should().BeTrue();
        lease!.Dispose();
    }

    /// <summary>Verifies ACK progress is monotonic and level success requires terminal acceptance.</summary>
    [Fact]
    public async Task LevelCalibrationUsesProgressAndTerminalAcknowledgement()
    {
        using var fixture = CreateFixture();
        var start = fixture.Service.StartLevelAsync(vehicleId, TestContext.Current.CancellationToken);
        await fixture.CommandSent.Task;

        await fixture.PublishAckAsync(MavResult.InProgress, 45);
        fixture.Service.Current.State.Should().Be(CalibrationWorkflowState.Completing);
        fixture.Service.Current.Progress.Should().Be(0.45);
        await fixture.PublishAckAsync(MavResult.InProgress, 20);
        fixture.Service.Current.Progress.Should().Be(0.45, "late progress must not move backwards");
        await fixture.PublishAckAsync(MavResult.Accepted);
        await start;

        fixture.Service.Current.State.Should().Be(CalibrationWorkflowState.Success);
        fixture.Service.Current.Instruction.Should().Contain("COMMAND_ACK");
    }

    /// <summary>Verifies explicit rejection, timeout, cancellation, and disconnect terminal states.</summary>
    [Fact]
    public async Task FailureTimeoutCancelAndDisconnectRemainRecoverable()
    {
        using (var rejected = CreateFixture())
        {
            var start = rejected.Service.StartLevelAsync(vehicleId, TestContext.Current.CancellationToken);
            await rejected.CommandSent.Task;
            await rejected.PublishAckAsync(MavResult.Denied);
            await start;
            rejected.Service.Current.State.Should().Be(CalibrationWorkflowState.Failed);
            rejected.Service.Reset();
            rejected.Service.Current.State.Should().Be(CalibrationWorkflowState.NotStarted);
        }

        using (var timedOut = CreateFixture(TimeSpan.FromMilliseconds(30)))
        {
            await timedOut.Service.StartSixPositionAsync(vehicleId, TestContext.Current.CancellationToken);
            timedOut.Service.Current.State.Should().Be(CalibrationWorkflowState.Failed);
            timedOut.Service.Current.FailureReason.Should().Contain("timed out");
        }

        using (var cancelled = CreateFixture())
        {
            var start = cancelled.Service.StartSixPositionAsync(vehicleId, TestContext.Current.CancellationToken);
            await cancelled.CommandSent.Task;
            await cancelled.PublishOrientationAsync(CalibrationOrientation.Level);
            await start;
            await cancelled.Service.CancelAsync(TestContext.Current.CancellationToken);
            cancelled.Service.Current.State.Should().Be(CalibrationWorkflowState.Cancelled);
            cancelled.Encoder.Received().EncodeCommandLong(1, 1, (ushort)MavCmd.AccelcalVehiclePos,
                Arg.Is<IReadOnlyList<float>>(values => values != null && values[0] == (float)AccelcalVehiclePos.Failed));
        }

        using (var disconnected = CreateFixture())
        {
            var start = disconnected.Service.StartSixPositionAsync(vehicleId, TestContext.Current.CancellationToken);
            await disconnected.CommandSent.Task;
            await disconnected.PublishOrientationAsync(CalibrationOrientation.Level);
            await start;
            disconnected.Active.SetOnline(false);
            disconnected.Service.Current.State.Should().Be(CalibrationWorkflowState.Disconnected);
            disconnected.Service.Reset();
            disconnected.Service.Current.State.Should().Be(CalibrationWorkflowState.NotStarted);
        }
    }

    /// <summary>Verifies the shared operation gate blocks ordinary commands during calibration.</summary>
    [Fact]
    public async Task CalibrationReservationBlocksCompetingVehicleCommand()
    {
        var gate = new VehicleOperationGate();
        gate.TryAcquire(vehicleId, "accelerometer calibration", out var lease).Should().BeTrue();
        var now = DateTimeOffset.UtcNow;
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(vehicleId).Returns(new VehicleSession(State(), endPoint, clock));
        var service = new VehicleCommandService(
            registry,
            Substitute.For<MissionPlanner.Library.EventHub.Abstractions.IDomainEventHub>(),
            Substitute.For<IMavLinkConnection>(),
            Substitute.For<IMavLinkCommandEncoder>(),
            Substitute.For<ICommandAckTracker>(),
            clock,
            new VehicleCommandPolicy(clock),
            new ArduPilotModeCatalog(),
            gate);

        var result = await service.ArmAsync(vehicleId, TestContext.Current.CancellationToken);

        result.Result.Should().Be(VehicleCommandResult.Busy);
        result.Message.Should().Contain("accelerometer calibration");
        lease!.Dispose();
    }

    /// <summary>Verifies MAVLink 2 COMMAND_ACK extension fields are retained for progress handling.</summary>
    [Fact]
    public void CommandAckDecoderRetainsProgressExtensions()
    {
        var payload = new byte[10];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, (ushort)MavCmd.PreflightCalibration);
        payload[2] = (byte)MavResult.InProgress;
        payload[3] = 57;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4), 1234);
        payload[8] = 255;
        payload[9] = 190;
        var frame = new MavLinkFrame(1, 1, endPoint, MessageIds.CommandAck, 0, payload, ReadOnlyMemory<byte>.Empty, DateTimeOffset.UtcNow);
        var decoder = new CommandAckMessageDecoder();

        decoder.TryDecode(frame, out var message).Should().BeTrue();
        var acknowledgement = message.Should().BeOfType<CommandAckMessage>().Subject;
        acknowledgement.Progress.Should().Be(57);
        acknowledgement.ResultParameter2.Should().Be(1234);
        acknowledgement.TargetSystemId.Should().Be(255);
        acknowledgement.TargetComponentId.Should().Be(190);
    }

    /// <summary>Verifies the ViewModel maps orientation images and stores evidence only on protocol success.</summary>
    [Fact]
    public void ViewModelProjectsOrientationAndConfirmedCompletion()
    {
        var active = Substitute.For<IActiveVehicleContext>();
        active.VehicleId.Returns(vehicleId);
        active.State.Returns(State());
        active.IsOnline.Returns(true);
        active.ConnectionCancellationToken.Returns(CancellationToken.None);
        var calibration = Substitute.For<IArduPilotCalibrationService>();
        calibration.Current.Returns(CalibrationSnapshot.Initial);
        var store = new MemoryCompletionStore();
        var registry = new VehicleParameterRegistry();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        using var viewModel = new AccelerometerSetupViewModel(
            new SetupWorkflowCatalog().Workflows.Single(item => item.Key == SetupWorkflowKey.Accelerometer),
            active,
            calibration,
            registry,
            store,
            new SetupWorkflowCatalog(),
            Substitute.For<IUserConfirmationService>(),
            clock,
            ImmediateDispatcher(),
            Substitute.For<ILogger<AccelerometerSetupViewModel>>());
        var waiting = new CalibrationSnapshot(vehicleId, AccelerometerCalibrationKind.SixPosition,
            CalibrationWorkflowState.WaitingForOrientation, CalibrationOrientation.NoseUp,
            new HashSet<CalibrationOrientation>(), 0.5, "Nose up");

        calibration.StateChanged += Raise.Event<EventHandler<CalibrationStateChangedEventArgs>>(
            calibration, new CalibrationStateChangedEventArgs(waiting));
        viewModel.OrientationImage.Should().Be("x_calibration07_x.jpg");
        store.GetAll().Should().BeEmpty();

        calibration.StateChanged += Raise.Event<EventHandler<CalibrationStateChangedEventArgs>>(
            calibration, new CalibrationStateChangedEventArgs(waiting with { State = CalibrationWorkflowState.Success, RequiredOrientation = null, Progress = 1 }));
        store.GetAll().Should().ContainSingle(item => item.Workflow == SetupWorkflowKey.Accelerometer);
    }

    private static async Task AdvanceAsync(CalibrationFixture fixture, CalibrationOrientation next)
    {
        await fixture.Service.ConfirmOrientationAsync(TestContext.Current.CancellationToken);
        await fixture.PublishOrientationAsync(next);
    }

    private static CalibrationFixture CreateFixture(TimeSpan? startTimeout = null)
    {
        var state = State();
        var active = new TestActiveVehicleContext(state);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(vehicleId).Returns(new VehicleSession(state, endPoint, clock));
        var eventHub = new EventHub(Substitute.For<ILogger<EventHub>>());
        var connection = Substitute.For<IMavLinkConnection>();
        var commandSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var encoder = Substitute.For<IMavLinkCommandEncoder>();
        encoder.EncodeCommandLong(Arg.Any<byte>(), Arg.Any<byte>(), Arg.Any<ushort>(), Arg.Any<IReadOnlyList<float>>())
            .Returns(_ =>
            {
                commandSent.TrySetResult();
                return [1, 2, 3];
            });
        var messageStore = new VehicleMessageStore(Options.Create(new VehicleMessageStoreOptions()));
        var gate = new VehicleOperationGate();
        var service = new ArduPilotCalibrationService(
            active,
            registry,
            eventHub,
            connection,
            encoder,
            gate,
            messageStore,
            new VehicleParameterRegistry(),
            Substitute.For<IVehicleParameterService>(),
            Options.Create(new CalibrationOptions { StartTimeout = startTimeout ?? TimeSpan.FromSeconds(2), LevelTimeout = TimeSpan.FromSeconds(2) }),
            Substitute.For<ILogger<ArduPilotCalibrationService>>());
        return new CalibrationFixture(service, active, eventHub, messageStore, gate, encoder, commandSent);
    }

    private static VehicleState State()
    {
        var now = DateTimeOffset.UtcNow;
        return new VehicleState(vehicleId, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
                VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null) with
            {
                Flight = new VehicleFlightState(0, 0, 4, VehicleMode.Stabilize, false,
                    LandedState: VehicleLandedState.OnGround, ObservedAt: now)
            };
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

    private sealed record CalibrationFixture(
        ArduPilotCalibrationService Service,
        TestActiveVehicleContext Active,
        EventHub EventHub,
        VehicleMessageStore MessageStore,
        VehicleOperationGate Gate,
        IMavLinkCommandEncoder Encoder,
        TaskCompletionSource CommandSent) : IDisposable
    {
        public Task PublishOrientationAsync(CalibrationOrientation orientation)
        {
            return PublishRawOrientationAsync((float)orientation);
        }

        public Task PublishRawOrientationAsync(float orientation)
        {
            return EventHub.PublishAsync<MavLinkMessage>(
                MavLinkEventTopics.ReceivedMessage,
                new CommandLongMessage(1, 1, endPoint, 255, 190, (ushort)MavCmd.AccelcalVehiclePos, 0,
                    orientation, 0, 0, 0, 0, 0, 0, DateTimeOffset.UtcNow),
                TestContext.Current.CancellationToken);
        }

        public Task PublishAckAsync(MavResult result, byte progress = byte.MaxValue)
        {
            return EventHub.PublishAsync<MavLinkMessage>(
                MavLinkEventTopics.ReceivedMessage,
                new CommandAckMessage(1, 1, endPoint, (ushort)MavCmd.PreflightCalibration, (byte)result,
                    DateTimeOffset.UtcNow, progress),
                TestContext.Current.CancellationToken);
        }

        public void Dispose()
        {
            Service.Dispose();
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

        public void SetOnline(bool online)
        {
            var previous = Current;
            var nextState = Current.State! with { Connection = Current.State!.Connection with { State = online ? VehicleConnectionState.Online : VehicleConnectionState.Offline } };
            Current = new ActiveVehicleSnapshot(nextState.VehicleId, nextState);
            if (!online)
            {
                lifetime.Cancel();
            }

            Changed?.Invoke(this, new ActiveVehicleChangedEventArgs(previous, Current));
        }
    }

    private sealed class MemoryCompletionStore : ISetupCompletionStore
    {
        private readonly List<SetupCompletionEvidence> values = [];

        public IReadOnlyList<SetupCompletionEvidence> GetAll()
        {
            return values;
        }

        public void Save(SetupCompletionEvidence evidence)
        {
            values.Add(evidence);
        }

        public void Remove(string vehicleKey, SetupWorkflowKey workflow)
        {
            values.RemoveAll(item => item.VehicleKey == vehicleKey && item.Workflow == workflow);
        }
    }
}
