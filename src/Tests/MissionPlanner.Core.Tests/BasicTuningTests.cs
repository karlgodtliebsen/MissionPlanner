using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Tuning;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Library;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Test.Support.Configuration;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies firmware-curated Basic Tuning selection, validation, files, and safe writes.</summary>
public sealed class BasicTuningTests
{
    private readonly ITestOutputHelper output;

    //private readonly ServiceProvider serviceProvider;
    private ILogger<BasicTuningTests>? logger;
    private readonly IServiceCollection services;

    /// <summary>
    /// Initializes a new instance of the <see cref="BasicTuningTests"/> class.
    /// </summary>
    /// <param name="output">The test output helper.</param>
    public BasicTuningTests(ITestOutputHelper output)
    {
        this.output = output;
        services = TestConfigurator
            .AddTestConfiguration(output);

        services.AddSingleton<IVehicleParameterService, TestParameterService>();
        services.AddSingleton<IVehicleParameterMetadataService, TestVehicleParameterMetadataService>();
        services.AddSingleton<IActiveVehicleContext, TestActiveVehicleContext>();
        services.AddSingleton(Options.Create(new ParameterEditSessionOptions { ReadbackTimeout = TimeSpan.FromSeconds(1) }));
    }

    private Fixture CreateFixture(FirmwareFamily family, params (string Name, float Value)[] parameters)
    {
        var state = State(family);
        services.AddSingleton<VehicleState>(provider => state);

        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseTestConfiguration();

        logger = serviceProvider.GetRequiredService<ILogger<BasicTuningTests>>();
        var registry = serviceProvider.GetRequiredService<IVehicleParameterRegistry>();
        foreach (var parameter in parameters)
        {
            registry.StoreParameter(
                state.VehicleId,
                new VehicleParameter(parameter.Name, parameter.Value, MavParamType.Real32, 0, (ushort)parameters.Length),
                TestContext.Current.CancellationToken);
        }

        var metadata = parameters.ToDictionary(
            parameter => parameter.Name,
            parameter => new ParameterMetadata(
                parameter.Name,
                parameter.Name,
                $"Description for {parameter.Name}",
                null,
                null,
                "0 1000",
                null,
                null,
                "1",
                "Standard",
                false,
                false),
            StringComparer.Ordinal);

        var metadataService = serviceProvider.GetRequiredService<IVehicleParameterMetadataService>() as TestVehicleParameterMetadataService;
        DomainException.ThrowIfNull(metadataService);
        metadataService.SetMetadata(metadata);

        var parameterService = serviceProvider.GetRequiredService<IVehicleParameterService>() as TestParameterService;
        DomainException.ThrowIfNull(parameterService);

        var activeVehicle = serviceProvider.GetRequiredService<IActiveVehicleContext>() as TestActiveVehicleContext;
        DomainException.ThrowIfNull(activeVehicle);


        var factory = serviceProvider.GetRequiredService<IParameterEditSessionFactory>() as ParameterEditSessionFactory;
        DomainException.ThrowIfNull(factory);


        var service = serviceProvider.GetRequiredService<IBasicTuningService>() as BasicTuningService;
        DomainException.ThrowIfNull(service);

        return new Fixture(state.VehicleId, activeVehicle, parameterService, factory, service, serviceProvider);
    }

    /// <summary>Verifies all supported firmware families have useful, explained, unit-bearing catalogs.</summary>
    /// <param name="family">The supported family.</param>
    [Theory]
    [InlineData(FirmwareFamily.ArduCopter)]
    [InlineData(FirmwareFamily.ArduPlane)]
    [InlineData(FirmwareFamily.Rover)]
    [InlineData(FirmwareFamily.ArduSub)]
    public void CatalogProvidesCuratedFamilyProfile(FirmwareFamily family)
    {
        var profile = new BasicTuningProfileCatalog().GetProfile(family);

        profile.Should().NotBeNull();
        profile!.Groups.Should().NotBeEmpty();
        profile.Groups.SelectMany(group => group.Fields).Should().OnlyContain(field =>
            !string.IsNullOrWhiteSpace(field.Description) &&
            !string.IsNullOrWhiteSpace(field.FallbackUnits) &&
            !field.ExpertOnly &&
            (!field.HasAuthoritativeRecommendation || !string.IsNullOrWhiteSpace(field.RecommendationSource)));
    }

    /// <summary>Verifies parameter presence hides unsupported fields and resolves a justified older alias.</summary>
    [Fact]
    public async Task WorkspaceUsesPresenceAndOrderedAliasResolution()
    {
        using var fixture = CreateFixture(
            FirmwareFamily.Rover,
            ("CRUISE_SPEED", 3),
            ("ATC_ACCEL_MAX", 1));

        var workspace = await fixture.Service.OpenAsync(fixture.VehicleId, TestContext.Current.CancellationToken);

        workspace.Should().NotBeNull();
        workspace!.Groups.SelectMany(group => group.Fields)
            .Should().ContainSingle(field => field.Definition.Key == "wp-speed")
            .Which.ParameterName.Should().Be("CRUISE_SPEED");
        workspace.PresentedParameterNames.Should().Contain("ATC_ACCEL_MAX");
        workspace.PresentedParameterNames.Should().NotContain("WP_SPEED");
        workspace.PresentedParameterNames.Should().NotContain("PILOT_SPEED");
    }

    /// <summary>Verifies coupled ordering and speed/acceleration validation prevents an unsafe group write.</summary>
    [Fact]
    public async Task CoupledValidationPreventsUnsafeGroupApply()
    {
        using var fixture = CreateFixture(
            FirmwareFamily.ArduCopter,
            ("PILOT_SPEED_UP", 200),
            ("PILOT_SPEED_DN", 100),
            ("PILOT_ACCEL_Z", 100));
        var workspace = (await fixture.Service.OpenAsync(fixture.VehicleId, TestContext.Current.CancellationToken))!;
        workspace.Session.TrySetPending("PILOT_SPEED_DN", 250, out var _).Should().BeTrue();
        workspace.Session.TrySetPending("PILOT_ACCEL_Z", 0, out var _).Should().BeTrue();

        var result = await fixture.Service.ApplyGroupAsync(
            workspace,
            "climb-descent",
            TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        result.ParameterReport.Should().BeNull();
        result.ValidationIssues.Should().HaveCount(2);

        fixture.ParameterService.Writes.Should().BeEmpty();
    }

    /// <summary>Verifies import/export is restricted to the profile and invalid imports are atomic.</summary>
    [Fact]
    public async Task ImportExportIncludesOnlyPresentedParametersAndIsAtomic()
    {
        using var fixture = CreateFixture(
            FirmwareFamily.ArduPlane,
            ("THR_MIN", 10),
            ("TRIM_THROTTLE", 45),
            ("THR_MAX", 100),
            ("EXPERT_RAW", 7));
        var workspace = (await fixture.Service.OpenAsync(fixture.VehicleId, TestContext.Current.CancellationToken))!;
        workspace.Session.TrySetPending("TRIM_THROTTLE", 50, out var _).Should().BeTrue();

        var exported = fixture.Service.Export(workspace);
        var document = JsonNode.Parse(exported)!.AsObject();
        var values = document["values"]!.AsObject();
        values["THR_MIN"] = 20;
        values["TRIM_THROTTLE"] = 55;
        values["EXPERT_RAW"] = 99;

        var imported = fixture.Service.Import(workspace, document.ToJsonString());

        imported.Success.Should().BeTrue();
        imported.ImportedCount.Should().Be(3);
        imported.IgnoredNames.Should().Equal("EXPERT_RAW");
        workspace.Session.GetField("THR_MIN")!.PendingValue.Should().Be(20);
        workspace.Session.GetField("TRIM_THROTTLE")!.PendingValue.Should().Be(55);
        exported.Should().NotContain("EXPERT_RAW");

        values["THR_MIN"] = 90;
        values["TRIM_THROTTLE"] = 80;
        var rejected = fixture.Service.Import(workspace, document.ToJsonString());

        rejected.Success.Should().BeFalse();
        workspace.Session.GetField("THR_MIN")!.PendingValue.Should().Be(20);
        workspace.Session.GetField("TRIM_THROTTLE")!.PendingValue.Should().Be(55);
    }

    /// <summary>Verifies a group applies only its fields and confirms registry readback.</summary>
    [Fact]
    public async Task GroupApplyUsesSharedConfirmedSession()
    {
        using var fixture = CreateFixture(
            FirmwareFamily.ArduCopter,
            ("LOIT_SPEED", 500),
            ("LOIT_ACC_MAX", 250),
            ("WPNAV_SPEED", 600));
        var workspace = (await fixture.Service.OpenAsync(fixture.VehicleId, TestContext.Current.CancellationToken))!;
        workspace.Session.TrySetPending("LOIT_SPEED", 550, out var _).Should().BeTrue();
        workspace.Session.TrySetPending("WPNAV_SPEED", 650, out var _).Should().BeTrue();

        var result = await fixture.Service.ApplyGroupAsync(workspace, "loiter", TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        result.ParameterReport!.Confirmed.Should().Equal("LOIT_SPEED");
        fixture.ParameterService.Writes.Should().Equal("LOIT_SPEED");
        workspace.Session.GetField("LOIT_SPEED")!.LiveValue.Should().Be(550);
        workspace.Session.GetField("WPNAV_SPEED")!.IsModified.Should().BeTrue();
    }

    /// <summary>Verifies the page projects supported fields, ignores telemetry churn, and reacts to disconnect.</summary>
    [Fact]
    public async Task ViewModelUsesProfileLifecycleWithoutTelemetryReloads()
    {
        using var fixture = CreateFixture(
            FirmwareFamily.ArduSub,
            ("PILOT_SPEED_UP", 200),
            ("PILOT_SPEED_DN", 100),
            ("PILOT_ACCEL_Z", 100));
        var dispatcher = Substitute.For<IDispatcher>();
        dispatcher.Dispatch(Arg.Any<Action>()).Returns(call =>
        {
            call.Arg<Action>()!();
            return true;
        });
        var confirmation = Substitute.For<IUserConfirmationService>();
        confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        using var viewModel = new BasicTuningTabViewModel(
            fixture.ActiveVehicle,
            fixture.Service,
            new ParametersFileHandler(Substitute.For<IFileSaver>()),
            confirmation,
            dispatcher,
            NullLogger<BasicTuningTabViewModel>.Instance);

        await viewModel.ActivateAsync();
        await WaitUntilAsync(() => viewModel.Groups.Count > 0);
        var firstGroup = viewModel.Groups[0];
        var online = fixture.ActiveVehicle.State!;

        fixture.ActiveVehicle.Set(online with
        {
            Flight = online.Flight with
            {
                IsArmed = true
            }
        });

        viewModel.Groups[0].Should().BeSameAs(firstGroup);

        fixture.ActiveVehicle.Set(online with
        {
            Connection = online.Connection with
            {
                State = VehicleConnectionState.Offline
            }
        });
        await WaitUntilAsync(() => viewModel.Groups.Count == 0);

        viewModel.IsConnected.Should().BeFalse();
        viewModel.StatusMessage.Should().Contain("Connect a vehicle");
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
            new VehicleId(1, 1),
            0,
            family == FirmwareFamily.ArduPlane ? (byte)1 : (byte)2,
            3,
            0,
            4,
            3,
            VehicleConnectionState.Online,
            DateTimeOffset.UtcNow,
            VehicleMode.Stabilize,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
        var firmware = new VehicleFirmwareIdentity(
            family,
            state.VehicleType,
            3,
            new FirmwareSemanticVersion(4, 6, 0, FirmwareReleaseType.Official),
            "abcdef01",
            0,
            1,
            2,
            3,
            4,
            "basic-tuning-test");
        return state with
        {
            Identity = state.Identity with
            {
                Firmware = firmware
            }
        };
    }

    private sealed record Fixture(
        VehicleId VehicleId,
        TestActiveVehicleContext ActiveVehicle,
        TestParameterService ParameterService,
        ParameterEditSessionFactory Factory,
        BasicTuningService Service,
        ServiceProvider ServiceProvider) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose()
        {
            ServiceProvider.Dispose();
        }
    }
}

/// <summary>Provides an in-memory parameter service for configuration tests.</summary>
/// <param name="registry">The registry updated when a test writes a parameter.</param>
public sealed class TestParameterService(IVehicleParameterRegistry registry) : IVehicleParameterService
{
    /// <summary>Gets the parameter names written by the test subject.</summary>
    public List<string> Writes { get; } = [];

    /// <inheritdoc />
    public Task<bool> RequestParameterListAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> RequestParameterAsync(VehicleId vehicleId, string parameterName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> RequestParameterByIndexAsync(VehicleId vehicleId, ushort parameterIndex, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> SetParameterAsync(VehicleId vehicleId, string parameterName, float value, MavParamType paramType, CancellationToken cancellationToken = default)
    {
        Writes.Add(parameterName);
        registry.StoreParameter(vehicleId, new VehicleParameter(parameterName, value, paramType, 0, 1), cancellationToken);
        return Task.FromResult(true);
    }
}

/// <summary>Provides mutable active-vehicle state for configuration tests.</summary>
/// <param name="state">The initial active vehicle state.</param>
public sealed class TestActiveVehicleContext(VehicleState state) : IActiveVehicleContext
{
    private CancellationTokenSource lifetime = new();

    /// <inheritdoc />
    public ActiveVehicleSnapshot Current { get; private set; } = new(state.VehicleId, state);

    /// <inheritdoc />
    public VehicleId? VehicleId => Current.VehicleId;

    /// <inheritdoc />
    public VehicleState? State => Current.State;

    /// <inheritdoc />
    public bool IsOnline => Current.IsOnline;

    /// <inheritdoc />
    public CancellationToken ConnectionCancellationToken => lifetime.Token;

    /// <inheritdoc />
    public event Action<ActiveVehicleChangedEventArgs>? Changed;

    /// <summary>Replaces the active state and publishes the corresponding context change.</summary>
    /// <param name="next">The replacement vehicle state.</param>
    public void Set(VehicleState next)
    {
        var previous = Current;
        Current = new ActiveVehicleSnapshot(next.VehicleId, next);
        if (previous.VehicleId != Current.VehicleId || previous.IsOnline != Current.IsOnline)
        {
            lifetime.Cancel();
            lifetime.Dispose();
            lifetime = new CancellationTokenSource();
            if (!Current.IsOnline)
            {
                lifetime.Cancel();
            }
        }

        Changed?.Invoke(new ActiveVehicleChangedEventArgs(previous, Current));
    }
}

/// <summary>Provides mutable in-memory firmware parameter metadata for configuration tests.</summary>
public sealed class TestVehicleParameterMetadataService : IVehicleParameterMetadataService
{
    private Dictionary<string, ParameterMetadata> metadate = [];

    /// <summary>Replaces the metadata returned to the test subject.</summary>
    /// <param name="metadata">The metadata indexed by parameter name.</param>
    public void SetMetadata(IReadOnlyDictionary<string, ParameterMetadata> metadata)

    {
        metadate = new Dictionary<string, ParameterMetadata>(metadata);
    }

    /// <inheritdoc />
    public Task<ParameterMetadata?> GetMetadataAsync(VehicleId vehicleId, string parameterName, CancellationToken cancellationToken = default)
    {
        metadate.TryGetValue(parameterName, out var metadata);
        return Task.FromResult(metadata);
    }

    /// <inheritdoc />
    public Task<ParameterMetadata?> GetMetadataAsync(VehicleType vehicleType, string parameterName, CancellationToken cancellationToken = default)
    {
        metadate.TryGetValue(parameterName, out var metadata);
        return Task.FromResult(metadata);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, ParameterMetadata>> GetAllMetadataAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, ParameterMetadata> md = metadate;
        return Task.FromResult(md);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, ParameterMetadata>> GetAllMetadataAsync(VehicleType vehicleType, CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<string, ParameterMetadata> md = metadate;
        return Task.FromResult(md);
    }

    /// <inheritdoc />
    public Task RefreshMetadataAsync(VehicleType vehicleType, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
