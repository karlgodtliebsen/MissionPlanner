using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies compass discovery, validation, and the onboard calibration state machine.</summary>
public sealed class CompassSetupTests
{
    private static readonly VehicleId vehicleId = new(1, 1);
    private static readonly TransportEndPoint endPoint = new("test");

    /// <summary>Verifies two compasses drive an acceptance-gated success with a quality summary.</summary>
    [Fact]
    public async Task TwoCompassCalibrationRequiresExplicitAcceptance()
    {
        using var fixture = CreateFixture();
        var start = fixture.Service.StartAsync(vehicleId, false, TestContext.Current.CancellationToken);
        await fixture.CommandSent.Task;
        await fixture.PublishAckAsync(MavResult.Accepted);
        await start;

        fixture.Service.Current.State.Should().Be(CompassCalibrationWorkflowState.Running);
        await fixture.PublishProgressAsync(0, 0b11, MagCalStatus.MagCalRunningStepOne, 40);
        await fixture.PublishProgressAsync(1, 0b11, MagCalStatus.MagCalRunningStepOne, 60);
        fixture.Service.Current.OverallProgress.Should().BeApproximately(0.5, 0.01);

        await fixture.PublishReportAsync(0, 0b11, MagCalStatus.MagCalSuccess, 0, 5);
        fixture.Service.Current.State.Should().Be(CompassCalibrationWorkflowState.Running, "the second compass has not reported yet");
        await fixture.PublishReportAsync(1, 0b11, MagCalStatus.MagCalSuccess, 0, 7);

        fixture.Service.Current.State.Should().Be(CompassCalibrationWorkflowState.PendingAcceptance);
        fixture.Service.Current.RequiresAcceptance.Should().BeTrue();
        fixture.Service.Current.QualitySummary.Should().Contain("Compass 1").And.Contain("Compass 2");

        await fixture.Service.AcceptAsync(TestContext.Current.CancellationToken);
        fixture.Service.Current.State.Should().Be(CompassCalibrationWorkflowState.Success);
        fixture.Encoder.Received().EncodeCommandLong(1, 1, (ushort)MavCmd.DoAcceptMagCal, Arg.Any<IReadOnlyList<float>>());
    }

    /// <summary>Verifies an auto-saved success completes without an acceptance step.</summary>
    [Fact]
    public async Task AutoSavedSuccessCompletesWithoutAcceptance()
    {
        using var fixture = CreateFixture();
        var start = fixture.Service.StartAsync(vehicleId, true, TestContext.Current.CancellationToken);
        await fixture.CommandSent.Task;
        await fixture.PublishProgressAsync(0, 0b1, MagCalStatus.MagCalRunningStepTwo, 90);
        await start;
        await fixture.PublishReportAsync(0, 0b1, MagCalStatus.MagCalSuccess, 1, 4);

        fixture.Service.Current.State.Should().Be(CompassCalibrationWorkflowState.Success);
        fixture.Service.Current.OverallProgress.Should().Be(1);
    }

    /// <summary>Verifies an explicit failure report ends the workflow in the failed state.</summary>
    [Fact]
    public async Task FailedReportEndsCalibration()
    {
        using var fixture = CreateFixture();
        var start = fixture.Service.StartAsync(vehicleId, false, TestContext.Current.CancellationToken);
        await fixture.CommandSent.Task;
        await fixture.PublishProgressAsync(0, 0b1, MagCalStatus.MagCalRunningStepOne, 30);
        await start;
        await fixture.PublishReportAsync(0, 0b1, MagCalStatus.MagCalFailed, 0, 40);

        fixture.Service.Current.State.Should().Be(CompassCalibrationWorkflowState.Failed);
        fixture.Service.Current.FailureReason.Should().Contain("compass 1");
        fixture.Service.Reset();
        fixture.Service.Current.State.Should().Be(CompassCalibrationWorkflowState.NotStarted);
    }

    /// <summary>Verifies cancel sends the protocol cancel and disconnect leaves a recoverable state.</summary>
    [Fact]
    public async Task CancelAndDisconnectRemainRecoverable()
    {
        using (var cancelled = CreateFixture())
        {
            var start = cancelled.Service.StartAsync(vehicleId, false, TestContext.Current.CancellationToken);
            await cancelled.CommandSent.Task;
            await cancelled.PublishProgressAsync(0, 0b1, MagCalStatus.MagCalRunningStepOne, 10);
            await start;
            await cancelled.Service.CancelAsync(TestContext.Current.CancellationToken);
            cancelled.Service.Current.State.Should().Be(CompassCalibrationWorkflowState.Cancelled);
            cancelled.Encoder.Received().EncodeCommandLong(1, 1, (ushort)MavCmd.DoCancelMagCal, Arg.Any<IReadOnlyList<float>>());
        }

        using (var disconnected = CreateFixture())
        {
            var start = disconnected.Service.StartAsync(vehicleId, false, TestContext.Current.CancellationToken);
            await disconnected.CommandSent.Task;
            await disconnected.PublishProgressAsync(0, 0b1, MagCalStatus.MagCalRunningStepOne, 10);
            await start;
            disconnected.Active.SetOnline(false);
            disconnected.Service.Current.State.Should().Be(CompassCalibrationWorkflowState.Disconnected);
        }
    }

    /// <summary>Verifies sparse compass slots, duplicate device IDs, and priority gaps are surfaced.</summary>
    [Fact]
    public async Task InventoryDiscoversSparseSlotsAndReportsIssues()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "COMPASS_DEV_ID", 100);
        Store(registry, "COMPASS_USE", 1);
        Store(registry, "COMPASS_EXTERNAL", 0);
        Store(registry, "COMPASS_ORIENT", 0);
        Store(registry, "COMPASS_DEV_ID3", 300); // Sparse slot: slot 2 is absent.
        Store(registry, "COMPASS_USE3", 1);
        Store(registry, "COMPASS_PRIO1_ID", 100); // Slot 3 is enabled but unranked.
        var service = CreateConfigurationService(registry, true);

        var inventory = await service.GetInventoryAsync(vehicleId, TestContext.Current.CancellationToken);

        inventory.Compasses.Select(compass => compass.Index).Should().Equal(1, 3);
        inventory.Compasses.Single(compass => compass.Index == 1).IsPrimary.Should().BeTrue();
        inventory.Compasses.Single(compass => compass.Index == 1).Healthy.Should().BeTrue();
        inventory.Compasses.Single(compass => compass.Index == 3).Priority.Should().Be(0);
        inventory.Issues.Should().Contain(issue => issue.Message.Contains("not present in the priority list"));
        inventory.OrientationOptions.Should().NotBeEmpty();
    }

    /// <summary>Verifies duplicate compass device IDs are flagged as a configuration conflict.</summary>
    [Fact]
    public async Task InventoryFlagsDuplicateDeviceIds()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "COMPASS_DEV_ID", 100);
        Store(registry, "COMPASS_USE", 1);
        Store(registry, "COMPASS_DEV_ID2", 100); // Same device ID as slot 1.
        Store(registry, "COMPASS_USE2", 1);
        var service = CreateConfigurationService(registry, null);

        var inventory = await service.GetInventoryAsync(vehicleId, TestContext.Current.CancellationToken);

        inventory.Issues.Should().Contain(issue => issue.Message.Contains("same device ID"));
    }

    /// <summary>Verifies the disable-safety policy protects the last enabled compass.</summary>
    [Fact]
    public async Task DisableSafetyProtectsOnlyEnabledCompass()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "COMPASS_DEV_ID", 100);
        Store(registry, "COMPASS_USE", 1);
        Store(registry, "COMPASS_DEV_ID2", 200);
        Store(registry, "COMPASS_USE2", 0);
        var service = CreateConfigurationService(registry, null);

        var inventory = await service.GetInventoryAsync(vehicleId, TestContext.Current.CancellationToken);

        service.WouldDisableOnlyEnabledCompass(inventory, 1).Should().BeTrue();
        service.WouldDisableOnlyEnabledCompass(inventory, 2).Should().BeFalse("compass 2 is already disabled");
    }

    /// <summary>Verifies the ViewModel projects calibration and records evidence only on protocol success.</summary>
    [Fact]
    public void ViewModelRecordsEvidenceOnConfirmedSuccess()
    {
        var active = Substitute.For<IActiveVehicleContext>();
        active.VehicleId.Returns(vehicleId);
        active.State.Returns(State());
        active.IsOnline.Returns(true);
        active.ConnectionCancellationToken.Returns(CancellationToken.None);
        var calibration = Substitute.For<IArduPilotCompassCalibrationService>();
        calibration.Current.Returns(CompassCalibrationSnapshot.Initial);
        var store = new MemoryCompletionStore();
        var registry = new VehicleParameterRegistry();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        using var viewModel = new CompassSetupViewModel(
            new SetupWorkflowCatalog().Workflows.Single(item => item.Key == SetupWorkflowKey.Compass),
            active,
            Substitute.For<ICompassConfigurationService>(),
            calibration,
            registry,
            store,
            new SetupWorkflowCatalog(),
            Substitute.For<IUserConfirmationService>(),
            clock,
            ImmediateDispatcher(),
            Substitute.For<ILogger<CompassSetupViewModel>>());
        viewModel.Activate();

        var running = new CompassCalibrationSnapshot(vehicleId, CompassCalibrationWorkflowState.Running,
            [new CompassCalibrationProgress(0, CompassCalibrationStatus.Running, 50, 1)], [], 0.5, "Rotating", false);
        calibration.StateChanged += Raise.Event<EventHandler<CompassCalibrationStateChangedEventArgs>>(
            calibration, new CompassCalibrationStateChangedEventArgs(running));
        viewModel.ProgressSummary.Should().Contain("Compass 1");
        store.GetAll().Should().BeEmpty();

        calibration.StateChanged += Raise.Event<EventHandler<CompassCalibrationStateChangedEventArgs>>(
            calibration, new CompassCalibrationStateChangedEventArgs(running with { State = CompassCalibrationWorkflowState.Success, OverallProgress = 1 }));
        store.GetAll().Should().ContainSingle(item => item.Workflow == SetupWorkflowKey.Compass);
    }

    private static CompassConfigurationService CreateConfigurationService(VehicleParameterRegistry registry, bool? magnetometerHealthy)
    {
        var active = Substitute.For<IActiveVehicleContext>();
        active.VehicleId.Returns(vehicleId);
        active.IsOnline.Returns(true);
        active.State.Returns(State(magnetometerHealthy));
        var metadata = Substitute.For<IVehicleParameterMetadataService>();
        metadata.GetAllMetadataAsync(vehicleId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, ParameterMetadata>());
        return new CompassConfigurationService(
            active,
            registry,
            metadata,
            Substitute.For<IVehicleParameterService>(),
            Substitute.For<ILogger<CompassConfigurationService>>());
    }

    private static void Store(VehicleParameterRegistry registry, string name, float value)
    {
        registry.StoreParameter(vehicleId, new VehicleParameter(name, value, MavLink.Parameters.MavParamType.Real32, 0, 1), CancellationToken.None);
    }

    private static CompassFixture CreateFixture()
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
        var service = new ArduPilotCompassCalibrationService(
            active,
            registry,
            eventHub,
            connection,
            encoder,
            new VehicleOperationGate(),
            Options.Create(new CompassCalibrationOptions { StartTimeout = TimeSpan.FromSeconds(2) }),
            Substitute.For<ILogger<ArduPilotCompassCalibrationService>>());
        return new CompassFixture(service, active, eventHub, encoder, commandSent);
    }

    private static VehicleState State(bool? magnetometerHealthy = null)
    {
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(vehicleId, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
                VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null) with
        {
            Flight = new VehicleFlightState(0, 0, 4, VehicleMode.Stabilize, false,
                    LandedState: VehicleLandedState.OnGround, ObservedAt: now)
        };
        if (magnetometerHealthy is { } healthy)
        {
            state = state with { Health = VehicleHealthState.Empty with { SensorsPresent = 0x04, SensorsHealthy = healthy ? 0x04u : 0u } };
        }

        return state;
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

    private sealed record CompassFixture(
        ArduPilotCompassCalibrationService Service,
        TestActiveVehicleContext Active,
        EventHub EventHub,
        IMavLinkCommandEncoder Encoder,
        TaskCompletionSource CommandSent) : IDisposable
    {
        public Task PublishAckAsync(MavResult result)
        {
            return EventHub.PublishAsync<MavLinkMessage>(
                MavLinkEventTopics.ReceivedMessage,
                new CommandAckMessage(1, 1, endPoint, (ushort)MavCmd.DoStartMagCal, (byte)result, DateTimeOffset.UtcNow),
                TestContext.Current.CancellationToken);
        }

        public Task PublishProgressAsync(byte compassId, byte calMask, MagCalStatus status, byte pct)
        {
            return EventHub.PublishAsync<MavLinkMessage>(
                MavLinkEventTopics.ReceivedMessage,
                new MagCalProgressMessage(1, 1, endPoint, compassId, calMask, (byte)status, 1, pct, new byte[10], 0, 0, 0, DateTimeOffset.UtcNow),
                TestContext.Current.CancellationToken);
        }

        public Task PublishReportAsync(byte compassId, byte calMask, MagCalStatus status, byte autosaved, float fitness)
        {
            return EventHub.PublishAsync<MavLinkMessage>(
                MavLinkEventTopics.ReceivedMessage,
                new MagCalReportMessage(1, 1, endPoint, compassId, calMask, (byte)status, autosaved, fitness,
                    0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 1, DateTimeOffset.UtcNow),
                TestContext.Current.CancellationToken);
        }

        public void Dispose()
        {
            Service.Dispose();
        }
    }

    private sealed class TestActiveVehicleContext(VehicleState state) : IActiveVehicleContext
    {
        private readonly CancellationTokenSource lifetime = new();

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
