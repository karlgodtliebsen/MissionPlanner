using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.ConfigTuning.Tabs;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Tuning;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Transport;
using NSubstitute;
using ParameterWireType = MissionPlanner.MavLink.Parameters.MavParamType;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies advanced descriptor expansion, validation, comparisons, copies, and response context.</summary>
public sealed class ExtendedTuningTests
{
    /// <summary>Verifies each supported family exposes structured advanced categories.</summary>
    /// <param name="family">The firmware family.</param>
    [Theory]
    [InlineData(FirmwareFamily.ArduCopter)]
    [InlineData(FirmwareFamily.ArduPlane)]
    [InlineData(FirmwareFamily.Rover)]
    [InlineData(FirmwareFamily.ArduSub)]
    public void CatalogSelectsAdvancedFamilyProfile(FirmwareFamily family)
    {
        var profile = new ExtendedTuningProfileCatalog().GetProfile(family);

        profile.Should().NotBeNull();
        profile!.Descriptors.Should().NotBeEmpty();
        profile.Descriptors.Should().OnlyContain(descriptor =>
            !string.IsNullOrWhiteSpace(descriptor.Category) &&
            !string.IsNullOrWhiteSpace(descriptor.ExpertWarning));
        profile.Descriptors.Should().Contain(descriptor => descriptor.Category == "Filters");
    }

    /// <summary>Verifies repeated rate-axis and gyro-instance descriptors expand to exact parameter names.</summary>
    [Fact]
    public void CatalogExpandsAxesAndInstances()
    {
        var catalog = new ExtendedTuningProfileCatalog();
        var profile = catalog.GetProfile(FirmwareFamily.ArduCopter)!;

        var rate = catalog.Expand(profile.Descriptors.Single(descriptor => descriptor.Key == "rate-pid"));
        var gyro = catalog.Expand(profile.Descriptors.Single(descriptor => descriptor.Key == "gyro-filters"));

        rate.Should().HaveCount(24);
        rate.Select(item => item.Parameter.ParameterNames.Single()).Should().Contain([
            "ATC_RAT_RLL_P", "ATC_RAT_PIT_FF", "ATC_RAT_YAW_FLTD"
        ]);
        gyro.Select(item => item.Parameter.ParameterNames.Single()).Should().Equal(
            "INS_GYRO_FILTER", "INS_GYRO2_FILTER", "INS_GYRO3_FILTER");
    }

    /// <summary>Verifies only present advanced fields load and PID/notch coupled rules reject unsafe combinations.</summary>
    [Fact]
    public async Task WorkspaceUsesPresenceAndValidatesPidAndFilterRelationships()
    {
        using var fixture = CreateFixture(
            FirmwareFamily.ArduCopter,
            ("ATC_RAT_RLL_I", 0.1f),
            ("ATC_RAT_RLL_IMAX", 0f),
            ("INS_HNTCH_FREQ", 50f),
            ("INS_HNTCH_BW", 60f));
        var workspace = (await fixture.Service.OpenAsync(fixture.VehicleId, TestContext.Current.CancellationToken))!;

        workspace.Groups.SelectMany(group => group.Fields).Should().HaveCount(4);
        fixture.Service.ValidateGroup(workspace, "rate-pid").Should().ContainSingle()
            .Which.Message.Should().Contain("integrator limit");
        fixture.Service.ValidateGroup(workspace, "harmonic-notch").Should().ContainSingle()
            .Which.Message.Should().Contain("bandwidth");
    }

    /// <summary>Verifies axis copy is non-mutating until its explicit preview is applied.</summary>
    [Fact]
    public async Task CopyAxisRequiresPreviewAndAppliesMatchingComponents()
    {
        using var fixture = CreateFixture(
            FirmwareFamily.ArduCopter,
            ("ATC_RAT_RLL_P", 1f),
            ("ATC_RAT_RLL_I", 0.2f),
            ("ATC_RAT_RLL_IMAX", 0.5f),
            ("ATC_RAT_PIT_P", 0.5f),
            ("ATC_RAT_PIT_I", 0.1f),
            ("ATC_RAT_PIT_IMAX", 0.4f));
        var workspace = (await fixture.Service.OpenAsync(fixture.VehicleId, TestContext.Current.CancellationToken))!;

        var preview = fixture.Service.PreviewCopyAxis(workspace, "rate-pid", "RLL", "PIT");

        preview.Changes.Should().HaveCount(3);
        workspace.Session.GetField("ATC_RAT_PIT_P")!.PendingValue.Should().Be(0.5);

        var applied = fixture.Service.ApplyCopyAxisPreview(workspace, preview);

        applied.Success.Should().BeTrue();
        workspace.Session.GetField("ATC_RAT_PIT_P")!.PendingValue.Should().Be(1);
        workspace.Session.GetField("ATC_RAT_PIT_I")!.PendingValue.Should().BeApproximately(0.2, 0.0001);
        workspace.Session.GetField("ATC_RAT_PIT_IMAX")!.PendingValue.Should().BeApproximately(0.5, 0.0001);
    }

    /// <summary>Verifies normalized comparison uses the largest pending axis magnitude per component.</summary>
    [Fact]
    public async Task AxisComparisonNormalizesPendingValues()
    {
        using var fixture = CreateFixture(
            FirmwareFamily.ArduCopter,
            ("ATC_RAT_RLL_P", 2f),
            ("ATC_RAT_PIT_P", 1f),
            ("ATC_RAT_YAW_P", 0.5f));
        var workspace = (await fixture.Service.OpenAsync(fixture.VehicleId, TestContext.Current.CancellationToken))!;

        var comparison = fixture.Service.CompareAxes(workspace, "rate-pid");

        comparison.Single(item => item.Axis == "RLL").NormalizedMagnitude.Should().Be(1);
        comparison.Single(item => item.Axis == "PIT").NormalizedMagnitude.Should().Be(0.5);
        comparison.Single(item => item.Axis == "YAW").NormalizedMagnitude.Should().Be(0.25);
    }

    /// <summary>Verifies large repeated descriptor expansion is linear and remains responsive.</summary>
    [Fact]
    public void LargeDescriptorExpansionRemainsResponsive()
    {
        var descriptor = new AdvancedTuningDescriptor(
            "large",
            "Performance",
            "Large descriptor",
            "Synthetic performance descriptor.",
            "TEST_{axis}{instance}",
            Enumerable.Range(0, 100).Select(index => $"A{index}").ToArray(),
            Enumerable.Range(1, 10).ToArray(),
            Enumerable.Range(0, 10).Select(index => new AdvancedTuningComponent($"C{index}", $"Component {index}", "Test component.", "gain")).ToArray(),
            [],
            "Expert warning.");
        var stopwatch = Stopwatch.StartNew();

        var expanded = new ExtendedTuningProfileCatalog().Expand(descriptor);

        stopwatch.Stop();
        expanded.Should().HaveCount(10_000);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    /// <summary>Verifies PID_TUNING telemetry is captured as read-only latest-per-axis context.</summary>
    [Fact]
    public async Task ResponseMetricsCaptureLatestPidTelemetry()
    {
        Func<MavLinkMessage, CancellationToken, Task>? receiver = null;
        var eventHub = Substitute.For<IEventHub>();
        eventHub.SubscribeAsync(
                MavLinkEventTopics.ReceivedMessage,
                Arg.Do<Func<MavLinkMessage, CancellationToken, Task>>(value => receiver = value))
            .Returns(Substitute.For<IDisposable>());
        using var service = new ControlResponseMetricsService(eventHub);
        var received = new List<ControlResponseMetric>();
        service.Changed += (_, args) => received.Add(args.Metric);

        await receiver!(new PidTuningMessage(
            1,
            1,
            new TransportEndPoint("test"),
            2,
            10,
            8,
            1,
            2,
            3,
            4,
            0,
            0,
            DateTimeOffset.UtcNow), CancellationToken.None);

        service.GetMetrics(new VehicleId(1, 1)).Should().ContainSingle().Which.Error.Should().Be(2);
        received.Should().ContainSingle();
    }

    /// <summary>Verifies the page keeps advanced rows lazy and materializes only search matches.</summary>
    [Fact]
    public async Task ViewModelSearchMaterializesOnlyMatchingAdvancedRows()
    {
        using var fixture = CreateFixture(
            FirmwareFamily.ArduCopter,
            ("ATC_RAT_RLL_P", 1f),
            ("ATC_RAT_PIT_P", 0.8f),
            ("INS_HNTCH_FREQ", 50f));
        var metrics = Substitute.For<IControlResponseMetricsService>();
        metrics.GetMetrics(fixture.VehicleId).Returns([
            new ControlResponseMetric(fixture.VehicleId, 1, 10, 9, 1, 0, 0, 0, 0, DateTimeOffset.UtcNow)
        ]);
        var confirmation = Substitute.For<IUserConfirmationService>();
        confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        var dispatcher = Substitute.For<IDispatcher>();
        dispatcher.Dispatch(Arg.Any<Action>()).Returns(call =>
        {
            call.Arg<Action>()!();
            return true;
        });
        using var viewModel = new ExtendedTuningTabViewModel(
            fixture.ActiveVehicle,
            fixture.Service,
            metrics,
            confirmation,
            dispatcher,
            NullLogger<ExtendedTuningTabViewModel>.Instance);

        viewModel.Activate();
        await WaitUntilAsync(() => viewModel.Groups.Count > 0);
        var rate = viewModel.Groups.Single(group => group.Key == "rate-pid");
        rate.Fields.Should().BeEmpty();
        rate.IsExpanded.Should().BeFalse();

        viewModel.SearchText = "ATC_RAT_RLL_P";

        viewModel.VisibleGroups.Should().ContainSingle().Which.Should().BeSameAs(rate);
        rate.IsExpanded.Should().BeTrue();
        rate.Fields.Should().ContainSingle().Which.ParameterName.Should().Be("ATC_RAT_RLL_P");
        viewModel.ResponseMetrics.Should().ContainSingle();
    }

    private static Fixture CreateFixture(FirmwareFamily family, params (string Name, float Value)[] parameters)
    {
        var state = State(family);
        var activeVehicle = new TestActiveVehicleContext(state);
        var registry = new VehicleParameterRegistry();
        foreach (var parameter in parameters)
        {
            registry.StoreParameter(
                state.VehicleId,
                new VehicleParameter(parameter.Name, parameter.Value, ParameterWireType.Real32, 0, (ushort)parameters.Length),
                CancellationToken.None);
        }

        var metadata = parameters.ToDictionary(
            item => item.Name,
            item => new ParameterMetadata(
                item.Name,
                item.Name,
                $"Description for {item.Name}",
                null,
                null,
                "0 1000",
                null,
                null,
                "0.01",
                "Expert",
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
        var service = new ExtendedTuningService(
            new ExtendedTuningProfileCatalog(),
            factory,
            registry,
            NullLogger<ExtendedTuningService>.Instance);
        return new Fixture(state.VehicleId, activeVehicle, factory, service);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static VehicleState State(FirmwareFamily family)
    {
        var state = new VehicleState(
            new VehicleId(1, 1), 0, 2, 3, 0, 4, 3,
            VehicleConnectionState.Online, DateTimeOffset.UtcNow, VehicleMode.Stabilize,
            false, null, null, null, null, null, null, null, null);
        var firmware = new VehicleFirmwareIdentity(
            family, 2, 3, new FirmwareSemanticVersion(4, 6, 0, FirmwareReleaseType.Official),
            "abcdef01", 0, 1, 2, 3, 4, "extended-tuning-test");
        return state with { Identity = state.Identity with { Firmware = firmware } };
    }

    private sealed record Fixture(
        VehicleId VehicleId,
        TestActiveVehicleContext ActiveVehicle,
        ParameterEditSessionFactory Factory,
        ExtendedTuningService Service) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose()
        {
            Factory.Dispose();
        }
    }

    private sealed class TestParameterService(IVehicleParameterRegistry registry) : IVehicleParameterService
    {
        public Task<bool> RequestParameterListAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> RequestParameterAsync(VehicleId vehicleId, string parameterName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> RequestParameterByIndexAsync(VehicleId vehicleId, ushort parameterIndex, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> SetParameterAsync(
            VehicleId vehicleId,
            string parameterName,
            float value,
            ParameterWireType paramType,
            CancellationToken cancellationToken = default)
        {
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
