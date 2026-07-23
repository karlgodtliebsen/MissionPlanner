using CommunityToolkit.Maui.Storage;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Helpers;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.App.Views.ConfigTuning.Tabs;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Services;
using MissionPlanner.App.Views.Simulation;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Fences;
using MissionPlanner.Core.ConfigTuning.Osd;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.ConfigTuning.Tuning;
using MissionPlanner.Core.ConfigTuning.VendorDevices;
using MissionPlanner.Core.ConfigTuning.VendorDevices.CubeLan;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Replay;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Simulation;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Services.Abstractions;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies the vehicle-aware Setup workflow shell and completion evidence.</summary>
public sealed class SetupWorkspaceTests
{
    /// <summary>Verifies that the primary ArduPilot vehicle families expose frame setup.</summary>
    /// <param name="family">The firmware family under test.</param>
    [Theory]
    [InlineData(FirmwareFamily.ArduCopter)]
    [InlineData(FirmwareFamily.ArduPlane)]
    [InlineData(FirmwareFamily.Rover)]
    public void CatalogShowsFrameForPrimaryVehicleFamilies(FirmwareFamily family)
    {
        var catalog = new SetupWorkflowCatalog();

        var result = catalog.Evaluate(Snapshot(State(family)), EmptyParameters(), []);

        result.Single(item => item.Descriptor.Key == SetupWorkflowKey.Frame).IsVisible.Should().BeTrue();
        result.Single(item => item.Descriptor.Key == SetupWorkflowKey.Frame).State.Should().Be(SetupWorkflowState.NotStarted);
    }

    /// <summary>Verifies capability and parameter presence control optional-hardware relevance.</summary>
    [Fact]
    public void CatalogUsesCapabilitiesAndParameterPresenceForVisibility()
    {
        var catalog = new SetupWorkflowCatalog();
        var withoutCapability = State(FirmwareFamily.ArduCopter);
        var withCapability = State(FirmwareFamily.ArduCopter, (ulong)MavProtocolCapability.Ftp);

        catalog.Evaluate(Snapshot(withoutCapability), EmptyParameters(), [])
            .Single(item => item.Descriptor.Key == SetupWorkflowKey.OptionalHardware).IsVisible.Should().BeFalse();
        catalog.Evaluate(Snapshot(withCapability), EmptyParameters(), [])
            .Single(item => item.Descriptor.Key == SetupWorkflowKey.OptionalHardware).IsVisible.Should().BeTrue();
        catalog.Evaluate(Snapshot(withoutCapability), Parameters(("SERIAL1_PROTOCOL", 2)), [])
            .Single(item => item.Descriptor.Key == SetupWorkflowKey.OptionalHardware).IsVisible.Should().BeTrue();
    }

    /// <summary>Verifies local completion becomes a warning after firmware or parameters change.</summary>
    [Fact]
    public void CompletionEvidenceInvalidatesAfterVehicleConfigurationChanges()
    {
        var catalog = new SetupWorkflowCatalog();
        var original = State(FirmwareFamily.ArduCopter, versionMajor: 4);
        var parameters = Parameters(("FRAME_CLASS", 1));
        var evidence = catalog.CreateEvidence(SetupWorkflowKey.Firmware, original, parameters, DateTimeOffset.UtcNow);

        Evaluation(catalog, original, parameters, evidence).State.Should().Be(SetupWorkflowState.Completed);
        Evaluation(catalog, original, Parameters(("FRAME_CLASS", 2)), evidence).State.Should().Be(SetupWorkflowState.Warning);
        Evaluation(catalog, State(FirmwareFamily.ArduCopter, versionMajor: 5), parameters, evidence).State.Should().Be(SetupWorkflowState.Warning);
    }

    /// <summary>Verifies the shell owns selection and visibility without owning section ViewModels.</summary>
    [Fact]
    public void ViewModelTracksSelectionAndConnectionWithoutOwningSectionViewModels()
    {
        var context = new TestActiveVehicleContext(State(FirmwareFamily.ArduCopter, (ulong)MavProtocolCapability.Ftp));
        var parameters = new VehicleParameterRegistry();
        var dispatcher = ImmediateDispatcher();
        var confirmation = Substitute.For<IUserConfirmationService>();
        confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        using var viewModel = new MandatoryHardwareViewModel(
            context,
            parameters,
            new SetupWorkflowCatalog(),
            new MemoryCompletionStore(),
            Substitute.For<ISetupNavigationService>(),
            confirmation,
            clock,
            dispatcher,
            Substitute.For<ILogger<MandatoryHardwareViewModel>>());
        viewModel.Activate();
        viewModel.SelectedWorkflow = viewModel.Workflows.Single(item => item.Descriptor.Key == SetupWorkflowKey.Frame);
        viewModel.IsFrameSelected.Should().BeTrue();
        viewModel.IsFirmwareSelected.Should().BeFalse();

        context.Set(context.Current.State! with { Connection = context.Current.State!.Connection with { State = VehicleConnectionState.Offline } });

        viewModel.VehicleHeading.Should().Contain("disconnected");
        viewModel.Workflows.Should().OnlyContain(item => item.State == SetupWorkflowState.NotConnected);

        var reconnected = State(FirmwareFamily.ArduCopter, (ulong)MavProtocolCapability.Ftp);
        context.Set(reconnected);
        viewModel.VehicleHeading.Should().Contain("ArduCopter");
        viewModel.Workflows.Should().Contain(item =>
            item.State == SetupWorkflowState.Available || item.State == SetupWorkflowState.NotStarted);

        viewModel.Deactivate();
        var heading = viewModel.VehicleHeading;
        context.Set(reconnected with { Connection = reconnected.Connection with { State = VehicleConnectionState.Offline } });
        viewModel.VehicleHeading.Should().Be(heading);
    }

    /// <summary>Verifies telemetry-only snapshots do not rebuild the Setup workflow collection.</summary>
    [Fact]
    public void ViewModelIgnoresTelemetryOnlyActiveVehicleChanges()
    {
        var context = new TestActiveVehicleContext(State(FirmwareFamily.ArduCopter));
        var dispatcher = ImmediateDispatcher();
        using var viewModel = new MandatoryHardwareViewModel(
            context,
            new VehicleParameterRegistry(),
            new SetupWorkflowCatalog(),
            new MemoryCompletionStore(),
            Substitute.For<ISetupNavigationService>(),
            Substitute.For<IUserConfirmationService>(),
            Substitute.For<IDateTimeProvider>(),
            dispatcher,
            Substitute.For<ILogger<MandatoryHardwareViewModel>>());
        viewModel.Activate();
        viewModel.SelectedWorkflow = viewModel.Workflows.Single(item => item.Descriptor.Key == SetupWorkflowKey.Frame);
        var workflowItems = viewModel.Workflows.ToArray();
        var selectedWorkflow = viewModel.SelectedWorkflow;
        dispatcher.ClearReceivedCalls();

        var current = context.Current.State!;
        context.Set(current with { Connection = current.Connection with { LastHeartbeatAt = current.LastHeartbeatAt.AddSeconds(1) }, Flight = current.Flight with { IsArmed = true } });

        dispatcher.DidNotReceive().Dispatch(Arg.Any<Action>());
        viewModel.SelectedWorkflow.Should().BeSameAs(selectedWorkflow);
        viewModel.Workflows.Should().HaveCount(workflowItems.Length);
        for (var index = 0; index < workflowItems.Length; index++)
        {
            viewModel.Workflows[index].Should().BeSameAs(workflowItems[index]);
        }
    }

    /// <summary>Verifies Setup shell services resolve from the application dependency graph.</summary>
    [Fact]
    public async Task DependencyInjectionResolvesSetupWorkspace()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ApplicationSettings:Channel"] = "UDP", ["ApplicationSettings:BaudRate"] = "115200", ["ApplicationSettings:Host"] = "127.0.0.1", ["ApplicationSettings:Port"] = "14550" }).Build();
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<ISetupCompletionStore>());
        services.AddSingleton(Substitute.For<IFirmwarePackageCache>());
        services.AddSingleton(Substitute.For<IDispatcher>());
        services.AddSingleton(Substitute.For<IFileSaver>());
        services.AddApplicationConfiguration(configuration);
        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ISetupWorkflowCatalog>().Should().NotBeNull();
        provider.GetRequiredService<IArduPilotCalibrationService>().Should().NotBeNull();
        provider.GetRequiredService<ICompassConfigurationService>().Should().NotBeNull();
        provider.GetRequiredService<IArduPilotCompassCalibrationService>().Should().NotBeNull();
        provider.GetRequiredService<IRadioCalibrationService>().Should().NotBeNull();
        provider.GetRequiredService<IFlightModeConfigurationService>().Should().NotBeNull();
        provider.GetRequiredService<IBatteryConfigurationService>().Should().NotBeNull();
        provider.GetRequiredService<IActuatorTestService>().Should().NotBeNull();
        provider.GetRequiredService<IServoOutputConfigurationService>().Should().NotBeNull();
        provider.GetRequiredService<IOptionalHardwareService>().Should().NotBeNull();
        provider.GetRequiredService<IOptionalHardwareCatalog>().Modules.Should().NotBeEmpty();
        provider.GetRequiredService<ISafetyAssessmentService>().Should().NotBeNull();
        provider.GetRequiredService<ISetupSummaryService>().Should().NotBeNull();
        provider.GetRequiredService<ISetupNavigationService>().Should().NotBeNull();
        provider.GetRequiredService<MandatoryHardwareViewModel>().Should().NotBeNull();
        var firmwareViewModel = provider.GetRequiredService<FirmwareSetupViewModel>();
        firmwareViewModel.Descriptor.Key.Should().Be(SetupWorkflowKey.Firmware);
        provider.GetRequiredService<FirmwareSetupViewModel>().Should().NotBeSameAs(firmwareViewModel);
        provider.GetRequiredService<FrameSetupViewModel>().Descriptor.Key.Should().Be(SetupWorkflowKey.Frame);
        provider.GetRequiredService<AccelerometerSetupViewModel>().Descriptor.Key.Should().Be(SetupWorkflowKey.Accelerometer);
        provider.GetRequiredService<CompassSetupViewModel>().Descriptor.Key.Should().Be(SetupWorkflowKey.Compass);
        provider.GetRequiredService<RadioSetupViewModel>().Descriptor.Key.Should().Be(SetupWorkflowKey.Radio);
        provider.GetRequiredService<FlightModesSetupViewModel>().Descriptor.Key.Should().Be(SetupWorkflowKey.FlightModes);
        provider.GetRequiredService<BatterySetupViewModel>().Descriptor.Key.Should().Be(SetupWorkflowKey.Battery);
        provider.GetRequiredService<EscMotorSetupViewModel>().Descriptor.Key.Should().Be(SetupWorkflowKey.Esc);
        provider.GetRequiredService<ServoOutputSetupViewModel>().Descriptor.Key.Should().Be(SetupWorkflowKey.ServoOutput);
        provider.GetRequiredService<OptionalHardwareSetupViewModel>().Descriptor.Key.Should().Be(SetupWorkflowKey.OptionalHardware);
        provider.GetRequiredService<SafetySetupViewModel>().Descriptor.Key.Should().Be(SetupWorkflowKey.Safety);
        provider.GetRequiredService<SetupSummaryViewModel>().Descriptor.Key.Should().Be(SetupWorkflowKey.Summary);
        provider.GetRequiredService<IParameterEditSessionFactory>().Should().NotBeNull();
        provider.GetRequiredService<IConfigNavigationGuard>().Should().NotBeNull();
        provider.GetRequiredService<IFenceProtocolMapper>().Should().NotBeNull();
        provider.GetRequiredService<IFenceGeometryValidator>().Should().NotBeNull();
        provider.GetRequiredService<IFenceConfigurationService>().Should().NotBeNull();
        provider.GetRequiredService<IBasicTuningProfileCatalog>().Should().NotBeNull();
        provider.GetRequiredService<IBasicTuningService>().Should().NotBeNull();
        provider.GetRequiredService<IExtendedTuningProfileCatalog>().Should().NotBeNull();
        provider.GetRequiredService<IExtendedTuningService>().Should().NotBeNull();
        provider.GetRequiredService<IControlResponseMetricsService>().Should().NotBeNull();
        provider.GetRequiredService<IOsdConfigurationService>().Should().NotBeNull();
        provider.GetRequiredService<IPlannerSettingsStore>().Should().NotBeNull();
        provider.GetRequiredService<IPlannerSecretStore>().Should().NotBeNull();
        provider.GetRequiredService<IPlannerSettingsService>().Should().NotBeNull();
        provider.GetRequiredService<PlannerTabViewModel>().Should().NotBeNull();
        provider.GetRequiredService<IMavLinkWireMessageEncoder>().Should().NotBeNull();
        provider.GetRequiredService<IDeviceOperationClient>().Should().NotBeNull();
        provider.GetRequiredService<ICubeLanConfigurationCodec>().Should().NotBeNull();
        provider.GetRequiredService<IVendorDeviceAdapter<CubeLanConfiguration>>().Should().NotBeNull();
        provider.GetRequiredService<CubeLan8PortSwitchTabViewModel>().Should().NotBeNull();
        provider.GetRequiredService<GeoFenceTabViewModel>().Should().NotBeNull();
        provider.GetRequiredService<ISimulatorProfileStore>().Should().NotBeNull();
        provider.GetRequiredService<ISimulatorProfileService>().Should().NotBeNull();
        provider.GetRequiredService<ISimulatorProfileValidator>().Should().NotBeNull();
        provider.GetRequiredService<ISimulatorRuntime>().Should().BeOfType<ArduPilotSitlRuntime>();
        provider.GetRequiredService<ISimulationSessionManager>().Should().NotBeNull();
        provider.GetRequiredService<ISimulationSessionManagerFactory>().Should().NotBeNull();
        provider.GetRequiredService<ISimulationFleetAllocator>().Should().NotBeNull();
        provider.GetRequiredService<ISimulationFleetManager>().Should().NotBeNull();
        provider.GetRequiredService<ISimulationVehicleChannelRegistry>().Should().NotBeNull();
        provider.GetRequiredService<ISimulatorVehicleConnectionFactory>().Should().NotBeNull();
        provider.GetRequiredService<ISimulationOwnershipStore>().Should().NotBeNull();
        provider.GetRequiredService<ISimulatorOwnedProcessRecovery>().Should().BeOfType<LocalSimulatorOwnedProcessRecovery>();
        provider.GetRequiredService<ISimulationDiagnosticsService>().Should().NotBeNull();
        provider.GetRequiredService<ITelemetryLogReader>().Should().NotBeNull();
        provider.GetRequiredService<IReplayTelemetryPipeline>().Should().NotBeNull();
        provider.GetRequiredService<IReplaySessionManager>().Should().NotBeNull();
        provider.GetRequiredService<IReplayClock>().Should().BeSameAs(provider.GetRequiredService<IReplaySessionManager>());
        provider.GetRequiredService<IMavLinkTransmissionPolicy>().Should().BeOfType<ReplayTransmissionPolicy>();
        provider.GetRequiredService<ISimulationControlCatalog>().Should().NotBeNull();
        provider.GetRequiredService<ISimulationControlService>().Should().NotBeNull();
        provider.GetRequiredService<ISimulationScenarioPresetStore>().Should().NotBeNull();
        provider.GetRequiredService<ISimulationScenarioPresetService>().Should().NotBeNull();
        provider.GetRequiredService<ISimulationScenarioParser>().Should().NotBeNull();
        provider.GetRequiredService<ISimulationScenarioRunner>().Should().NotBeNull();
        provider.GetRequiredService<ISimulationScenarioReportExporter>().Should().NotBeNull();
        provider.GetRequiredService<IArduPilotFrameCatalog>().Should().NotBeNull();
        provider.GetRequiredService<IArduPilotLaunchPlanBuilder>().Should().NotBeNull();
        provider.GetRequiredService<ISimulationPortAllocator>().Should().NotBeNull();
        provider.GetRequiredService<ISimulatorVehicleConnection>().Should().NotBeNull();
        provider.GetRequiredService<ISimulatorProcessHost>().Should().NotBeNull();
        provider.GetRequiredService<ISitlManifestProvider>().Should().NotBeNull();
        provider.GetRequiredService<ISitlReleaseSelector>().Should().NotBeNull();
        provider.GetRequiredService<ISitlCachePathProvider>().Should().NotBeNull();
        provider.GetRequiredService<ISitlPlatformService>().Should().NotBeNull();
        provider.GetRequiredService<ISitlPackageManager>().Should().NotBeNull();
        provider.GetRequiredService<ISitlInstallationService>().Should().NotBeNull();
        provider.GetRequiredService<SimulationViewModel>().Should().NotBeNull();
    }

    private static SetupWorkflowEvaluation Evaluation(
        ISetupWorkflowCatalog catalog,
        VehicleState state,
        IReadOnlyDictionary<string, VehicleParameter> parameters,
        SetupCompletionEvidence evidence)
    {
        return catalog.Evaluate(Snapshot(state), parameters, [evidence]).Single(item => item.Descriptor.Key == SetupWorkflowKey.Firmware);
    }

    private static ActiveVehicleSnapshot Snapshot(VehicleState state)
    {
        return new ActiveVehicleSnapshot(state.VehicleId, state);
    }

    private static VehicleState State(FirmwareFamily family, ulong capabilities = 0, byte versionMajor = 4)
    {
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(new VehicleId(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now, VehicleMode.Stabilize,
            false, null, null, null, null, null, null, null, null);
        var firmware = new VehicleFirmwareIdentity(
            family,
            state.VehicleType,
            state.Autopilot,
            new FirmwareSemanticVersion(versionMajor, 0, 0, FirmwareReleaseType.Official),
            "abcdef01",
            capabilities,
            1,
            2,
            3,
            42,
            "vehicle-1");
        return state with { Identity = state.Identity with { Firmware = firmware } };
    }

    private static IReadOnlyDictionary<string, VehicleParameter> EmptyParameters()
    {
        return new Dictionary<string, VehicleParameter>();
    }

    private static IReadOnlyDictionary<string, VehicleParameter> Parameters(params (string Name, float Value)[] values)
    {
        return values.ToDictionary(
            item => item.Name,
            item => new VehicleParameter(item.Name, item.Value, MavParamType.Real32, 0, (ushort)values.Length),
            StringComparer.Ordinal);
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

    private sealed class MemoryCompletionStore : ISetupCompletionStore
    {
        private readonly List<SetupCompletionEvidence> evidence = [];

        public IReadOnlyList<SetupCompletionEvidence> GetAll()
        {
            return evidence.ToArray();
        }

        public void Save(SetupCompletionEvidence value)
        {
            Remove(value.VehicleKey, value.Workflow);
            evidence.Add(value);
        }

        public void Remove(string vehicleKey, SetupWorkflowKey workflow)
        {
            evidence.RemoveAll(item => item.VehicleKey == vehicleKey && item.Workflow == workflow);
        }
    }

    private sealed class TestActiveVehicleContext(VehicleState state) : IActiveVehicleContext
    {
        private CancellationTokenSource lifetime = new();

        public ActiveVehicleSnapshot Current { get; private set; } = Snapshot(state);

        public VehicleId? VehicleId => Current.VehicleId;

        public VehicleState? State => Current.State;

        public bool IsOnline => Current.IsOnline;

        public CancellationToken ConnectionCancellationToken => lifetime.Token;

        public event EventHandler<ActiveVehicleChangedEventArgs>? Changed;

        public void Set(VehicleState next)
        {
            var previous = Current;
            if (previous.IsOnline != (next.ConnectionState == VehicleConnectionState.Online))
            {
                lifetime.Cancel();
                lifetime.Dispose();
                lifetime = new CancellationTokenSource();
                if (next.ConnectionState != VehicleConnectionState.Online)
                {
                    lifetime.Cancel();
                }
            }

            Current = new ActiveVehicleSnapshot(next.VehicleId, next);
            Changed?.Invoke(this, new ActiveVehicleChangedEventArgs(previous, Current));
        }
    }
}
