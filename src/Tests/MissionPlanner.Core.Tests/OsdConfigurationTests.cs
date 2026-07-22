using System.Text.Json.Nodes;
using CommunityToolkit.Maui.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.App.Views.ConfigTuning.Tabs;
using MissionPlanner.Core.Configuration;
using MissionPlanner.Core.Configuration.Osd;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies OSD parameter discovery, layout safety, files, and confirmed writes.</summary>
public sealed class OsdConfigurationTests
{
    /// <summary>Verifies screen and custom-item discovery uses firmware parameter patterns and metadata bounds.</summary>
    [Fact]
    public async Task DiscoveryFindsScreensItemsAndCustomOptions()
    {
        using var fixture = CreateFixture(
            P("OSD1_ENABLE", 1),
            P("OSD1_OPTIONS", 1, description: "Dynamic item overlap options", bitmask: "0:Allow dynamic overlap"),
            P("OSD1_ALTITUDE_EN", 1),
            P("OSD1_ALTITUDE_X", 4, "0 49"),
            P("OSD1_ALTITUDE_Y", 3, "0 17"),
            P("OSD1_CUSTOM_STATUS_EN", 1),
            P("OSD1_CUSTOM_STATUS_X", 8, "0 49"),
            P("OSD1_CUSTOM_STATUS_Y", 5, "0 17"),
            P("OSD1_CUSTOM_STATUS_TYPE", 2),
            P("OSD2_BAT_VOLT_EN", 1),
            P("OSD2_BAT_VOLT_X", 2, "0 29"),
            P("OSD2_BAT_VOLT_Y", 2, "0 15"),
            P("OSD_UNITS", 0));

        var workspace = await fixture.Service.OpenAsync(fixture.VehicleId, TestContext.Current.CancellationToken);

        workspace.Should().NotBeNull();
        workspace!.Screens.Should().HaveCount(2);
        var first = workspace.Screens[0];
        first.GridWidth.Should().Be(50);
        first.GridHeight.Should().Be(18);
        first.SupportsDynamicOverlaps.Should().BeTrue();
        first.Items.Should().ContainSingle(item => item.Key == "CUSTOM_STATUS")
            .Which.AdditionalParameterNames.Should().Contain("OSD1_CUSTOM_STATUS_TYPE");
        workspace.GlobalParameterNames.Should().Contain("OSD_UNITS");
    }

    /// <summary>Verifies out-of-bounds coordinates and unsupported overlaps are blocking errors.</summary>
    [Fact]
    public async Task ValidationRejectsBoundsAndStaticOverlaps()
    {
        using var fixture = CreateFixture(
            P("OSD1_FIRST_EN", 1), P("OSD1_FIRST_X", 5, "0 29"), P("OSD1_FIRST_Y", 5, "0 15"),
            P("OSD1_SECOND_EN", 1), P("OSD1_SECOND_X", 5, "0 29"), P("OSD1_SECOND_Y", 5, "0 15"));
        var workspace = (await fixture.Service.OpenAsync(fixture.VehicleId, TestContext.Current.CancellationToken))!;
        workspace.Session.TrySetPending("OSD1_FIRST_X", 30, out _).Should().BeFalse();
        fixture.Service.ValidateScreen(workspace, 1).Should().Contain(issue => issue.Message.Contains("outside", StringComparison.Ordinal));
        workspace.Session.TrySetPending("OSD1_FIRST_X", 5, out _).Should().BeTrue();

        var overlap = fixture.Service.ValidateScreen(workspace, 1);

        overlap.Should().ContainSingle(issue => issue.Severity == OsdValidationSeverity.Error && issue.ItemKeys.Count == 2);
    }

    /// <summary>Verifies dynamic overlap support downgrades collision to a warning requiring explicit acknowledgement.</summary>
    [Fact]
    public async Task DynamicOverlapRequiresExplicitAcknowledgement()
    {
        using var fixture = CreateFixture(
            P("OSD1_OPTIONS", 1, description: "Supports dynamic overlap"),
            P("OSD1_FIRST_EN", 1), P("OSD1_FIRST_X", 5, "0 29"), P("OSD1_FIRST_Y", 5, "0 15"),
            P("OSD1_SECOND_EN", 1), P("OSD1_SECOND_X", 5, "0 29"), P("OSD1_SECOND_Y", 5, "0 15"));
        var workspace = (await fixture.Service.OpenAsync(fixture.VehicleId, TestContext.Current.CancellationToken))!;

        var blocked = await fixture.Service.ApplyScreenAsync(workspace, 1, false, TestContext.Current.CancellationToken);
        var accepted = await fixture.Service.ApplyScreenAsync(workspace, 1, true, TestContext.Current.CancellationToken);

        blocked.Success.Should().BeFalse();
        blocked.ValidationIssues.Should().ContainSingle(issue => issue.Severity == OsdValidationSeverity.Warning);
        accepted.Success.Should().BeTrue();
    }

    /// <summary>Verifies exact and keyboard-style movement is bounded and updates both pending coordinates.</summary>
    [Fact]
    public async Task MoveItemUpdatesPendingCoordinatesWithinGrid()
    {
        using var fixture = CreateFixture(
            P("OSD1_ALT_EN", 1), P("OSD1_ALT_X", 4, "0 29"), P("OSD1_ALT_Y", 3, "0 15"));
        var workspace = (await fixture.Service.OpenAsync(fixture.VehicleId, TestContext.Current.CancellationToken))!;

        fixture.Service.MoveItem(workspace, 1, "ALT", 5, 2).Should().BeNull();
        fixture.Service.MoveItem(workspace, 1, "ALT", -1, 2).Should().Contain("outside");

        workspace.Session.GetField("OSD1_ALT_X")!.PendingValue.Should().Be(5);
        workspace.Session.GetField("OSD1_ALT_Y")!.PendingValue.Should().Be(2);
    }

    /// <summary>Verifies layout files contain only discovered fields and invalid imports restore prior pending state.</summary>
    [Fact]
    public async Task ImportExportIsRestrictedAndAtomic()
    {
        using var fixture = CreateFixture(
            P("OSD1_ALT_EN", 1), P("OSD1_ALT_X", 4, "0 29"), P("OSD1_ALT_Y", 3, "0 15"),
            P("NOT_OSD", 10));
        var workspace = (await fixture.Service.OpenAsync(fixture.VehicleId, TestContext.Current.CancellationToken))!;
        workspace.Session.TrySetPending("OSD1_ALT_X", 6, out _).Should().BeTrue();
        var exported = fixture.Service.Export(workspace);
        var document = JsonNode.Parse(exported)!.AsObject();
        var values = document["values"]!.AsObject();
        values["UNKNOWN_OSD"] = 9;
        values["OSD1_ALT_X"] = 7;

        var imported = fixture.Service.Import(workspace, document.ToJsonString());

        imported.Success.Should().BeTrue();
        imported.IgnoredNames.Should().Equal("UNKNOWN_OSD");
        workspace.Session.GetField("OSD1_ALT_X")!.PendingValue.Should().Be(7);
        exported.Should().NotContain("NOT_OSD");

        values["OSD1_ALT_X"] = 99;
        var rejected = fixture.Service.Import(workspace, document.ToJsonString());

        rejected.Success.Should().BeFalse();
        workspace.Session.GetField("OSD1_ALT_X")!.PendingValue.Should().Be(7);
    }

    /// <summary>Verifies screen apply writes only modified screen fields and confirms registry readback.</summary>
    [Fact]
    public async Task ApplyUsesSharedSessionConfirmedReadback()
    {
        using var fixture = CreateFixture(
            P("OSD1_ALT_EN", 1), P("OSD1_ALT_X", 4, "0 29"), P("OSD1_ALT_Y", 3, "0 15"),
            P("OSD2_ALT_EN", 1), P("OSD2_ALT_X", 8, "0 29"), P("OSD2_ALT_Y", 6, "0 15"));
        var workspace = (await fixture.Service.OpenAsync(fixture.VehicleId, TestContext.Current.CancellationToken))!;
        fixture.Service.MoveItem(workspace, 1, "ALT", 9, 7).Should().BeNull();
        workspace.Session.TrySetPending("OSD2_ALT_X", 10, out _).Should().BeTrue();

        var result = await fixture.Service.ApplyScreenAsync(workspace, 1, false, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.ParameterReport!.Confirmed.Should().BeEquivalentTo("OSD1_ALT_X", "OSD1_ALT_Y");
        fixture.ParameterService.Writes.Should().BeEquivalentTo("OSD1_ALT_X", "OSD1_ALT_Y");
        workspace.Session.GetField("OSD2_ALT_X")!.IsModified.Should().BeTrue();
    }

    /// <summary>Verifies accessible movement commands and numeric placement stay synchronized with the preview.</summary>
    [Fact]
    public async Task ViewModelMovesSelectedItemAndRejectsInvalidNumericPlacement()
    {
        using var fixture = CreateFixture(
            P("OSD1_ALT_EN", 1), P("OSD1_ALT_X", 4, "0 29"), P("OSD1_ALT_Y", 3, "0 15"));
        var dispatcher = Substitute.For<IDispatcher>();
        dispatcher.Dispatch(Arg.Any<Action>()).Returns(call =>
        {
            call.Arg<Action>()!();
            return true;
        });
        var confirmation = Substitute.For<IUserConfirmationService>();
        confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        using var viewModel = new OnboardOsdTabViewModel(
            fixture.ActiveVehicle,
            fixture.Service,
            new ParametersFileHandler(Substitute.For<IFileSaver>()),
            confirmation,
            dispatcher,
            NullLogger<OnboardOsdTabViewModel>.Instance);

        viewModel.Activate();
        await WaitUntilAsync(() => viewModel.SelectedItem is not null);
        viewModel.MoveRightCommand.Execute(null);
        viewModel.MoveUpCommand.Execute(null);

        viewModel.SelectedItem!.Column.Should().Be(5);
        viewModel.SelectedItem.Row.Should().Be(2);
        viewModel.PreviewItems.Should().ContainSingle(item => item.Column == 5 && item.Row == 2);

        viewModel.SelectedItem.Column = 30;

        viewModel.SelectedItem.Column.Should().Be(5);
        viewModel.SelectedItem.ValidationError.Should().Contain("outside");
    }

    private static Fixture CreateFixture(params TestParameter[] parameters)
    {
        var state = State();
        var activeVehicle = new TestActiveVehicleContext(state);
        var registry = new VehicleParameterRegistry();
        foreach (var parameter in parameters)
        {
            registry.StoreParameter(
                state.VehicleId,
                new VehicleParameter(parameter.Name, parameter.Value, MavParamType.Real32, 0, (ushort)parameters.Length),
                CancellationToken.None);
        }

        var metadata = parameters.ToDictionary(
            item => item.Name,
            item => new ParameterMetadata(
                item.Name,
                item.Name.Replace('_', ' '),
                item.Description ?? $"Description for {item.Name}",
                null,
                null,
                item.Range ?? "0 1000",
                item.Name.EndsWith("_EN", StringComparison.Ordinal) ? "0:Disabled,1:Enabled" : null,
                item.Bitmask,
                "1",
                "Standard",
                false,
                false),
            StringComparer.Ordinal);
        var metadataService = Substitute.For<IVehicleParameterMetadataService>();
        metadataService.GetAllMetadataAsync(state.VehicleId, Arg.Any<CancellationToken>()).Returns(metadata);
        var parameterService = new TestParameterService(registry);
        var factory = new ParameterEditSessionFactory(
            activeVehicle,
            registry,
            parameterService,
            metadataService,
            Options.Create(new ParameterEditSessionOptions { ReadbackTimeout = TimeSpan.FromSeconds(1) }),
            NullLoggerFactory.Instance);
        var service = new OsdConfigurationService(factory, registry, NullLogger<OsdConfigurationService>.Instance);
        return new Fixture(state.VehicleId, activeVehicle, parameterService, factory, service);
    }

    private static TestParameter P(
        string name,
        float value,
        string? range = null,
        string? description = null,
        string? bitmask = null) => new(name, value, range, description, bitmask);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static VehicleState State()
    {
        var state = new VehicleState(
            new VehicleId(1, 1), 0, 2, 3, 0, 4, 3,
            VehicleConnectionState.Online, DateTimeOffset.UtcNow, VehicleMode.Stabilize,
            false, null, null, null, null, null, null, null, null);
        var firmware = new VehicleFirmwareIdentity(
            FirmwareFamily.ArduCopter, 2, 3,
            new FirmwareSemanticVersion(4, 6, 0, FirmwareReleaseType.Official),
            "abcdef01", 0, 1, 2, 3, 4, "osd-test");
        return state with { Identity = state.Identity with { Firmware = firmware } };
    }

    private sealed record TestParameter(
        string Name,
        float Value,
        string? Range,
        string? Description,
        string? Bitmask);

    private sealed record Fixture(
        VehicleId VehicleId,
        TestActiveVehicleContext ActiveVehicle,
        TestParameterService ParameterService,
        ParameterEditSessionFactory Factory,
        OsdConfigurationService Service) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => Factory.Dispose();
    }

    private sealed class TestParameterService(IVehicleParameterRegistry registry) : IVehicleParameterService
    {
        public List<string> Writes { get; } = [];

        public Task<bool> RequestParameterListAsync(VehicleId vehicleId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> RequestParameterAsync(VehicleId vehicleId, string parameterName, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> RequestParameterByIndexAsync(VehicleId vehicleId, ushort parameterIndex, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<bool> SetParameterAsync(
            VehicleId vehicleId,
            string parameterName,
            float value,
            MavParamType paramType,
            CancellationToken cancellationToken = default)
        {
            Writes.Add(parameterName);
            registry.StoreParameter(vehicleId, new VehicleParameter(parameterName, value, paramType, 0, 1), cancellationToken);
            return Task.FromResult(true);
        }
    }

    private sealed class TestActiveVehicleContext(VehicleState state) : IActiveVehicleContext
    {
        public ActiveVehicleSnapshot Current { get; } = new(state.VehicleId, state);

        public VehicleId? VehicleId => Current.VehicleId;

        public VehicleState? State => Current.State;

        public bool IsOnline => true;

        public CancellationToken ConnectionCancellationToken => CancellationToken.None;

        public event EventHandler<ActiveVehicleChangedEventArgs>? Changed
        {
            add { }
            remove { }
        }
    }
}
