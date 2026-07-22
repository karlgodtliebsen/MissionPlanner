using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.InitSetup.Sections;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Parameters;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies metadata-driven frame discovery, guarded writes, rollback, and presentation.</summary>
public sealed class FrameConfigurationTests
{
    private static readonly VehicleId vehicleId = new(1, 1);

    /// <summary>Verifies family catalogs expose only parameters present in both live values and metadata.</summary>
    /// <param name="family">The reported firmware family.</param>
    /// <param name="expectedName">The expected family-specific class parameter.</param>
    [Theory]
    [InlineData(FirmwareFamily.ArduCopter, "FRAME_CLASS")]
    [InlineData(FirmwareFamily.ArduPlane, "Q_FRAME_CLASS")]
    [InlineData(FirmwareFamily.Rover, "FRAME_CLASS")]
    public async Task CatalogUsesFamilyParameterNamesAndMetadataPresence(FirmwareFamily family, string expectedName)
    {
        var registry = new VehicleParameterRegistry();
        registry.StoreParameter(vehicleId, Parameter(expectedName, 1), TestContext.Current.CancellationToken);
        registry.StoreParameter(vehicleId, Parameter("FRAME_TYPE", 1), TestContext.Current.CancellationToken);
        registry.StoreParameter(vehicleId, Parameter("Q_FRAME_TYPE", 1), TestContext.Current.CancellationToken);
        var metadata = Metadata((expectedName, "1:Quad,2:Hexa"));
        var service = CreateService(family, registry, metadata, new RecordingParameterService(registry));

        var result = await service.GetConfigurationAsync(vehicleId, TestContext.Current.CancellationToken);

        result.Settings.Should().ContainSingle().Which.Name.Should().Be(expectedName);
        result.Settings[0].Options.Select(item => item.Value).Should().Equal(1, 2);
    }

    /// <summary>Verifies successful frame writes are confirmed by registry readback and report reboot.</summary>
    [Fact]
    public async Task ApplyConfirmsReadbackAndRebootRequirement()
    {
        var registry = Registry(("FRAME_CLASS", 1), ("FRAME_TYPE", 1));
        var protocol = new RecordingParameterService(registry);
        var service = CreateService(
            FirmwareFamily.ArduCopter,
            registry,
            Metadata(("FRAME_CLASS", "1:Quad,2:Hexa", true), ("FRAME_TYPE", "0:Plus,1:X", false)),
            protocol);

        var result = await service.ApplyAsync(vehicleId,
        [
            new FrameParameterChange("FRAME_CLASS", 1, 2, MavParamType.Real32),
            new FrameParameterChange("FRAME_TYPE", 1, 0, MavParamType.Real32)
        ], TestContext.Current.CancellationToken);

        result.Status.Should().Be(FrameConfigurationApplyStatus.Succeeded);
        result.RequiresReboot.Should().BeTrue();
        registry.GetParameter(vehicleId, "FRAME_CLASS")!.Value.Should().Be(2);
        registry.GetParameter(vehicleId, "FRAME_TYPE")!.Value.Should().Be(0);
        protocol.SetNames.Should().Equal("FRAME_CLASS", "FRAME_TYPE");
    }

    /// <summary>Verifies a later write failure rolls previously confirmed values back.</summary>
    [Fact]
    public async Task PartialWriteFailureRollsBackConfirmedValues()
    {
        var registry = Registry(("FRAME_CLASS", 1), ("FRAME_TYPE", 1));
        var protocol = new RecordingParameterService(registry) { FailName = "FRAME_TYPE" };
        var service = CreateService(
            FirmwareFamily.ArduCopter,
            registry,
            Metadata(("FRAME_CLASS", "1:Quad,2:Hexa"), ("FRAME_TYPE", "0:Plus,1:X")),
            protocol);

        var result = await service.ApplyAsync(vehicleId,
        [
            new FrameParameterChange("FRAME_CLASS", 1, 2, MavParamType.Real32),
            new FrameParameterChange("FRAME_TYPE", 1, 0, MavParamType.Real32)
        ], TestContext.Current.CancellationToken);

        result.Status.Should().Be(FrameConfigurationApplyStatus.RolledBack);
        registry.GetParameter(vehicleId, "FRAME_CLASS")!.Value.Should().Be(1);
        protocol.SetNames.Should().Equal("FRAME_CLASS", "FRAME_TYPE", "FRAME_CLASS");
    }

    /// <summary>Verifies cancellation after a confirmed write reports an explicit partial state.</summary>
    [Fact]
    public async Task CancellationAfterWriteReportsPartialFailure()
    {
        var registry = Registry(("FRAME_CLASS", 1), ("FRAME_TYPE", 1));
        using var cancellation = new CancellationTokenSource();
        var protocol = new RecordingParameterService(registry) { CancelAfterFirstWrite = cancellation };
        var service = CreateService(
            FirmwareFamily.ArduCopter,
            registry,
            Metadata(("FRAME_CLASS", "1:Quad,2:Hexa"), ("FRAME_TYPE", "0:Plus,1:X")),
            protocol);

        var result = await service.ApplyAsync(vehicleId,
        [
            new FrameParameterChange("FRAME_CLASS", 1, 2, MavParamType.Real32),
            new FrameParameterChange("FRAME_TYPE", 1, 0, MavParamType.Real32)
        ], cancellation.Token);

        result.Status.Should().Be(FrameConfigurationApplyStatus.PartialFailure);
        result.ConfirmedParameters.Should().Equal("FRAME_CLASS");
        registry.GetParameter(vehicleId, "FRAME_TYPE")!.Value.Should().Be(1);
    }

    /// <summary>Verifies initial recommendations are exposed but never included in unrelated writes.</summary>
    [Fact]
    public async Task InitialRecommendationRequiresExplicitChange()
    {
        var registry = Registry(("FRAME_CLASS", 1), ("FRAME_TYPE", 1), ("ARMING_CHECK", 0));
        var protocol = new RecordingParameterService(registry);
        var service = CreateService(
            FirmwareFamily.ArduCopter,
            registry,
            Metadata(("FRAME_CLASS", "1:Quad,2:Hexa"), ("FRAME_TYPE", "0:Plus,1:X"), ("ARMING_CHECK", null)),
            protocol);

        var configuration = await service.GetConfigurationAsync(vehicleId, TestContext.Current.CancellationToken);
        var result = await service.ApplyAsync(vehicleId,
            [new FrameParameterChange("FRAME_CLASS", 1, 2, MavParamType.Real32)],
            TestContext.Current.CancellationToken);

        configuration.Recommendations.Should().ContainSingle().Which.Name.Should().Be("ARMING_CHECK");
        result.Succeeded.Should().BeTrue();
        registry.GetParameter(vehicleId, "ARMING_CHECK")!.Value.Should().Be(0);
        protocol.SetNames.Should().NotContain("ARMING_CHECK");
    }

    /// <summary>Verifies the ViewModel distinguishes current and pending values and records only confirmed success.</summary>
    [Fact]
    public async Task ViewModelRecordsEvidenceAfterConfirmedApply()
    {
        var state = State(FirmwareFamily.ArduCopter);
        var context = Substitute.For<IActiveVehicleContext>();
        context.VehicleId.Returns(vehicleId);
        context.State.Returns(state);
        context.IsOnline.Returns(true);
        context.ConnectionCancellationToken.Returns(CancellationToken.None);
        var frameService = Substitute.For<IFrameConfigurationService>();
        var setting = new FrameParameterSetting("FRAME_CLASS", "Frame Class", 1, MavParamType.Real32, true,
            [new FrameParameterOption(1, "Quad"), new FrameParameterOption(2, "Hexa")]);
        frameService.GetConfigurationAsync(vehicleId, Arg.Any<CancellationToken>())
            .Returns(new FrameConfigurationSnapshot(vehicleId, FirmwareFamily.ArduCopter, [setting], []));
        frameService.ApplyAsync(vehicleId, Arg.Any<IReadOnlyList<FrameParameterChange>>(), Arg.Any<CancellationToken>())
            .Returns(new FrameConfigurationApplyResult(FrameConfigurationApplyStatus.Succeeded, "Confirmed", ["FRAME_CLASS"], [], true));
        var registry = Registry(("FRAME_CLASS", 2));
        var completionStore = new MemoryCompletionStore();
        var confirmation = Substitute.For<IUserConfirmationService>();
        confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        using var viewModel = new FrameSetupViewModel(
            new SetupWorkflowCatalog().Workflows.Single(item => item.Key == SetupWorkflowKey.Frame),
            context,
            frameService,
            registry,
            completionStore,
            new SetupWorkflowCatalog(),
            confirmation,
            clock,
            ImmediateDispatcher(),
            Substitute.For<ILogger<FrameSetupViewModel>>());
        await viewModel.LoadAsync();
        viewModel.Settings[0].SelectedOption = viewModel.Settings[0].Options[1];
        viewModel.Settings[0].HasChange.Should().BeTrue();

        await viewModel.ApplyCommand.ExecuteAsync(null);

        viewModel.RebootRequired.Should().BeTrue();
        completionStore.GetAll().Should().ContainSingle(item => item.Workflow == SetupWorkflowKey.Frame);
    }

    private static FrameConfigurationService CreateService(
        FirmwareFamily family,
        IVehicleParameterRegistry registry,
        IReadOnlyDictionary<string, ParameterMetadata> metadata,
        IVehicleParameterService protocol)
    {
        var context = Substitute.For<IActiveVehicleContext>();
        context.VehicleId.Returns(vehicleId);
        context.State.Returns(State(family));
        context.IsOnline.Returns(true);
        var metadataService = Substitute.For<IVehicleParameterMetadataService>();
        metadataService.GetAllMetadataAsync(vehicleId, Arg.Any<CancellationToken>()).Returns(metadata);
        return new FrameConfigurationService(context, registry, metadataService, protocol, Substitute.For<ILogger<FrameConfigurationService>>());
    }

    private static VehicleState State(FirmwareFamily family)
    {
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(vehicleId, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now, VehicleMode.Stabilize,
            false, null, null, null, null, null, null, null, null);
        return state with { Identity = state.Identity with { Firmware = state.Identity.Firmware with { Family = family } } };
    }

    private static VehicleParameterRegistry Registry(params (string Name, float Value)[] values)
    {
        var registry = new VehicleParameterRegistry();
        foreach (var value in values)
        {
            registry.StoreParameter(vehicleId, Parameter(value.Name, value.Value), default);
        }

        return registry;
    }

    private static VehicleParameter Parameter(string name, float value)
    {
        return new VehicleParameter(name, value, MavParamType.Real32, 0, 10);
    }

    private static IReadOnlyDictionary<string, ParameterMetadata> Metadata(params (string Name, string? Values, bool Reboot)[] definitions)
    {
        return definitions.ToDictionary(
            item => item.Name,
            item => new ParameterMetadata(item.Name, item.Name, item.Name, null, null, "0 100", item.Values, null, null, "Standard", item.Reboot, false),
            StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, ParameterMetadata> Metadata(params (string Name, string? Values)[] definitions)
    {
        return Metadata(definitions.Select(item => (item.Name, item.Values, false)).ToArray());
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

    private sealed class RecordingParameterService(VehicleParameterRegistry registry) : IVehicleParameterService
    {
        public string? FailName { get; init; }

        public CancellationTokenSource? CancelAfterFirstWrite { get; init; }

        public List<string> SetNames { get; } = [];

        public Task<bool> RequestParameterListAsync(VehicleId id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> RequestParameterAsync(VehicleId id, string parameterName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> RequestParameterByIndexAsync(VehicleId id, ushort parameterIndex, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> SetParameterAsync(VehicleId id, string parameterName, float value, MavParamType paramType, CancellationToken cancellationToken = default)
        {
            SetNames.Add(parameterName);
            if (parameterName == FailName)
            {
                return Task.FromResult(false);
            }

            registry.StoreParameter(id, Parameter(parameterName, value), cancellationToken);
            if (SetNames.Count == 1)
            {
                CancelAfterFirstWrite?.Cancel();
            }

            return Task.FromResult(true);
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
