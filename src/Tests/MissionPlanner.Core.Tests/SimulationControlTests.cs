using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Simulation;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies documented, bounded, and instance-specific simulation controls.</summary>
[Trait("TestTier", "Unit")]
public sealed class SimulationControlTests
{
    /// <summary>Verifies built-in locations, units, and runtime value bounds are explicit and enforced.</summary>
    [Fact]
    public async Task CatalogAndServiceValidateLocationsUnitsAndRanges()
    {
        await using var fixture = CreateFixture((vehicleOne, "SIM_WIND_SPD", 0));

        fixture.Catalog.Locations.Should().OnlyContain(item =>
            item.Location.LatitudeDegrees >= -90 && item.Location.LatitudeDegrees <= 90 &&
            item.Location.LongitudeDegrees >= -180 && item.Location.LongitudeDegrees <= 180 &&
            item.Location.HeadingDegrees >= 0 && item.Location.HeadingDegrees <= 360);
        fixture.Catalog.Controls.Single(item => item.Key == "wind-speed").Unit.Should().Be("m/s");

        var apply = () => fixture.Service.ApplyAsync(
            "wind-speed", 101, null, false, TestContext.Current.CancellationToken);

        await apply.Should().ThrowAsync<ArgumentOutOfRangeException>();
        fixture.Writes.Should().BeEmpty();
    }

    /// <summary>Verifies discovery selects a live firmware alias and explains unsupported controls.</summary>
    [Fact]
    public async Task DiscoveryUsesLiveParameterPresenceAndReportsFirmwareVersion()
    {
        await using var fixture = CreateFixture((vehicleOne, "SIM_GPS_DISABLE", 0));

        var capabilities = await fixture.Service.DiscoverAsync(TestContext.Current.CancellationToken);

        var gps = capabilities.Single(item => item.Descriptor.Key == "gps-failure");
        gps.IsAvailable.Should().BeTrue();
        gps.ParameterName.Should().Be("SIM_GPS_DISABLE");
        gps.FirmwareVersion.Should().Be(new FirmwareSemanticVersion(4, 6, 0, FirmwareReleaseType.Official));
        var rangefinder = capabilities.Single(item => item.Descriptor.Key == "rangefinder-failure");
        rangefinder.IsAvailable.Should().BeFalse();
        rangefinder.Reason.Should().Contain("No bounded general-purpose rangefinder failure parameter");
    }

    /// <summary>Verifies hazardous controls require confirmation and reset automatically at their bound.</summary>
    [Fact]
    public async Task HazardRequiresConfirmationAndAutomaticallyResets()
    {
        await using var fixture = CreateFixture((vehicleOne, "SIM_RC_FAIL", 0));

        var unconfirmed = () => fixture.Service.ApplyAsync(
            "rc-failure", 1, TimeSpan.FromMilliseconds(40), false, TestContext.Current.CancellationToken);
        await unconfirmed.Should().ThrowAsync<InvalidOperationException>().WithMessage("*confirmation*");

        await fixture.Service.ApplyAsync(
            "rc-failure", 1, TimeSpan.FromMilliseconds(40), true, TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => fixture.Writes.Count == 2, TimeSpan.FromSeconds(2));

        fixture.Writes.Should().Equal(
            new ParameterWrite(vehicleOne, "SIM_RC_FAIL", 1),
            new ParameterWrite(vehicleOne, "SIM_RC_FAIL", 0));
        fixture.Service.Events.Select(item => item.Result).Should().ContainInOrder(
            SimulationScenarioEventResult.Applied,
            SimulationScenarioEventResult.AutoReset);
        fixture.Service.Events.Should().OnlyContain(item => item.VehicleId == vehicleOne);
    }

    /// <summary>Verifies an explicit reset cancels the pending automatic reset without a duplicate write.</summary>
    [Fact]
    public async Task ExplicitResetCancelsPendingAutomaticReset()
    {
        await using var fixture = CreateFixture((vehicleOne, "SIM_MAG1_FAIL", 0));

        await fixture.Service.ApplyAsync(
            "compass-failure", 1, TimeSpan.FromMilliseconds(250), true, TestContext.Current.CancellationToken);
        await fixture.Service.ResetAsync("compass-failure", TestContext.Current.CancellationToken);
        await Task.Delay(350, TestContext.Current.CancellationToken);

        fixture.Writes.Should().Equal(
            new ParameterWrite(vehicleOne, "SIM_MAG1_FAIL", 1),
            new ParameterWrite(vehicleOne, "SIM_MAG1_FAIL", 0));
        fixture.Service.Events.Last().Result.Should().Be(SimulationScenarioEventResult.Reset);
    }

    /// <summary>Verifies cancellation before an operation prevents any vehicle-changing write.</summary>
    [Fact]
    public async Task CancelledApplyDoesNotWrite()
    {
        await using var fixture = CreateFixture((vehicleOne, "SIM_WIND_DIR", 0));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var apply = () => fixture.Service.ApplyAsync("wind-direction", 90, null, false, cancellation.Token);

        await apply.Should().ThrowAsync<OperationCanceledException>();
        fixture.Writes.Should().BeEmpty();
    }

    /// <summary>Verifies a delayed reset never crosses from its original SITL session to another instance.</summary>
    [Fact]
    public async Task AutomaticResetNeverTargetsReplacementInstance()
    {
        await using var fixture = CreateFixture(
            (vehicleOne, "SIM_RC_FAIL", 0),
            (vehicleTwo, "SIM_RC_FAIL", 0));

        await fixture.Service.ApplyAsync(
            "rc-failure", 1, TimeSpan.FromMilliseconds(50), true, TestContext.Current.CancellationToken);
        fixture.SelectVehicle(vehicleTwo);
        await WaitUntilAsync(
            () => fixture.Service.Events.Any(item => item.Result == SimulationScenarioEventResult.Failed),
            TimeSpan.FromSeconds(2));

        fixture.Writes.Should().ContainSingle().Which.Should().Be(new ParameterWrite(vehicleOne, "SIM_RC_FAIL", 1));
        fixture.Writes.Should().NotContain(item => item.VehicleId == vehicleTwo);
        fixture.Service.Events.Last().Message.Should().Contain("exact simulation session");
    }

    /// <summary>Verifies scenario presets use their own versioned persistence and recover from corrupt content.</summary>
    [Fact]
    public async Task ScenarioPresetsPersistSeparatelyAndRecoverFromCorruption()
    {
        var store = new MemoryPresetStore("not-json");
        var service = new SimulationScenarioPresetService(
            store,
            Substitute.For<ILogger<SimulationScenarioPresetService>>());
        (await service.InitializeAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        var preset = new SimulationScenarioPreset(
            Guid.NewGuid(),
            "Crosswind",
            new SimulationLocation(55, 12, 5, 180),
            [new SimulationPresetControlValue("wind-speed", 8, null)]);

        await service.SaveAsync(preset, TestContext.Current.CancellationToken);

        store.Document.Should().Contain("\"version\": 1");
        var reloaded = new SimulationScenarioPresetService(
            store,
            Substitute.For<ILogger<SimulationScenarioPresetService>>());
        (await reloaded.InitializeAsync(TestContext.Current.CancellationToken)).Should().ContainSingle().Which.Should().BeEquivalentTo(preset);
    }

    private static readonly VehicleId vehicleOne = new(1, 1);
    private static readonly VehicleId vehicleTwo = new(2, 1);

    private static ControlFixture CreateFixture(params (VehicleId VehicleId, string Name, float Value)[] parameters)
    {
        var catalog = new SimulationControlCatalog();
        var registry = new VehicleParameterRegistry();
        foreach (var parameter in parameters)
        {
            registry.StoreParameter(
                parameter.VehicleId,
                new VehicleParameter(parameter.Name, parameter.Value, MavParamType.Real32, 0, 1),
                CancellationToken.None);
        }

        var writes = new List<ParameterWrite>();
        var parameterService = Substitute.For<IVehicleParameterService>();
        parameterService.RequestParameterAsync(Arg.Any<VehicleId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        parameterService.SetParameterAsync(
                Arg.Any<VehicleId>(),
                Arg.Any<string>(),
                Arg.Any<float>(),
                Arg.Any<MavParamType>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var vehicleId = call.ArgAt<VehicleId>(0);
                var name = call.ArgAt<string>(1);
                var value = call.ArgAt<float>(2);
                lock (writes)
                {
                    writes.Add(new ParameterWrite(vehicleId, name, value));
                }

                registry.StoreParameter(
                    vehicleId,
                    new VehicleParameter(name, value, call.ArgAt<MavParamType>(3), 0, 1),
                    CancellationToken.None);
                return Task.FromResult(true);
            });
        var connection = Substitute.For<IVehicleConnectionSession>();
        connection.ParameterService.Returns(parameterService);
        var vehicleRegistry = Substitute.For<IVehicleRegistry>();
        var firstSession = CreateVehicleSession(vehicleOne);
        var secondSession = CreateVehicleSession(vehicleTwo);
        vehicleRegistry.GetRequired(vehicleOne).Returns(firstSession);
        vehicleRegistry.GetRequired(vehicleTwo).Returns(secondSession);
        var now = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var manager = Substitute.For<ISimulationSessionManager>();
        var current = Snapshot(vehicleOne, now);
        manager.Current.Returns(_ => current);
        var service = new SimulationControlService(
            catalog,
            manager,
            connection,
            registry,
            vehicleRegistry,
            clock,
            Options.Create(new SimulationControlOptions { DiscoveryWaitMilliseconds = 0, ReadbackTimeoutSeconds = 1 }),
            Substitute.For<ILogger<SimulationControlService>>());
        return new ControlFixture(catalog, service, writes, vehicleId => current = Snapshot(vehicleId, now));
    }

    private static SimulationSessionSnapshot Snapshot(VehicleId vehicleId, DateTimeOffset now) => new(
        Guid.NewGuid(),
        SimulatorProfile.CreateDefault() with
        {
            LaunchSettings = ArduPilotLaunchSettings.Default with { SystemId = vehicleId.SystemId }
        },
        SimulationSessionState.Running,
        new SimulatorRuntimeIdentity($"sitl-{vehicleId.SystemId}", "test", null),
        [],
        now - TimeSpan.FromMinutes(1),
        null,
        "Running",
        null,
        [],
        vehicleId);

    private static VehicleSession CreateVehicleSession(VehicleId vehicleId)
    {
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(
            vehicleId,
            0,
            2,
            3,
            0,
            4,
            3,
            VehicleConnectionState.Online,
            now,
            VehicleMode.Unknown,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
        state = state with
        {
            Identity = state.Identity with
            {
                Firmware = state.Identity.Firmware with
                {
                    Family = FirmwareFamily.ArduCopter,
                    FlightVersion = new FirmwareSemanticVersion(4, 6, 0, FirmwareReleaseType.Official)
                }
            }
        };
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        return new VehicleSession(state, new TransportEndPoint("simulation-test"), clock);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!condition())
        {
            await Task.Delay(10, cancellation.Token);
        }
    }

    private sealed record ParameterWrite(VehicleId VehicleId, string Name, float Value);

    private sealed record ControlFixture(
        SimulationControlCatalog Catalog,
        SimulationControlService Service,
        List<ParameterWrite> Writes,
        Action<VehicleId> SelectVehicle) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Service.DisposeAsync();
    }

    private sealed class MemoryPresetStore(string? document) : ISimulationScenarioPresetStore
    {
        public string? Document { get; private set; } = document;

        public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Document);

        public ValueTask WriteAsync(string value, CancellationToken cancellationToken = default)
        {
            Document = value;
            return ValueTask.CompletedTask;
        }
    }
}
