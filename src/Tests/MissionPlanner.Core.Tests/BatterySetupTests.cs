using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Parameters;
using NSubstitute;
using MavParamType = MissionPlanner.MavLink.Parameters.MavParamType;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies battery discovery, calibration math, and failsafe validation.</summary>
public sealed class BatterySetupTests
{
    private static readonly VehicleId vehicleId = new(1, 1);

    /// <summary>Verifies sparse battery instances are discovered with live primary telemetry.</summary>
    [Fact]
    public async Task DiscoversSparseInstancesWithLiveTelemetry()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "BATT_MONITOR", 4);
        Store(registry, "BATT_CAPACITY", 5000);
        Store(registry, "BATT3_MONITOR", 4); // Sparse: instance 2 absent.
        var now = DateTimeOffset.UtcNow;
        var context = new TestActiveVehicleContext(State(now, voltage: 12.4, current: 8));
        var service = CreateService(context, registry, now);

        var configuration = await service.GetConfigurationAsync(vehicleId, TestContext.Current.CancellationToken);

        configuration.Instances.Select(instance => instance.Index).Should().Equal(1, 3);
        var primary = configuration.Instances.Single(instance => instance.Index == 1);
        primary.Live.HasTelemetry.Should().BeTrue();
        primary.Live.VoltageVolts.Should().Be(12.4);
        configuration.Instances.Single(instance => instance.Index == 3).Live.HasTelemetry.Should().BeFalse();
    }

    /// <summary>Verifies measured-vs-reference voltage calibration scales the multiplier.</summary>
    [Fact]
    public async Task VoltageCalibrationScalesMultiplier()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "BATT_MONITOR", 4);
        Store(registry, "BATT_VOLT_MULT", 10.0f);
        var now = DateTimeOffset.UtcNow;
        var service = CreateService(new TestActiveVehicleContext(State(now, 12.0, 0)), registry, now);

        var result = await service.CalibrateVoltageAsync(vehicleId, 1, measuredVolts: 12.0, referenceVolts: 12.6, TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        registry.GetParameter(vehicleId, "BATT_VOLT_MULT")!.Value.Should().BeApproximately(10.0f * (12.6f / 12.0f), 0.001f);
    }

    /// <summary>Verifies an invalid measured value is rejected before any write.</summary>
    [Fact]
    public async Task VoltageCalibrationRejectsNonPositiveMeasurement()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "BATT_MONITOR", 4);
        Store(registry, "BATT_VOLT_MULT", 10.0f);
        var now = DateTimeOffset.UtcNow;
        var service = CreateService(new TestActiveVehicleContext(State(now, 0, 0)), registry, now);

        var result = await service.CalibrateVoltageAsync(vehicleId, 1, measuredVolts: 0, referenceVolts: 12.6, TestContext.Current.CancellationToken);

        result.Success.Should().BeFalse();
        registry.GetParameter(vehicleId, "BATT_VOLT_MULT")!.Value.Should().Be(10.0f);
    }

    /// <summary>Verifies an out-of-order failsafe threshold cannot be saved.</summary>
    [Fact]
    public async Task InvalidFailsafeOrderingIsRejected()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "BATT_MONITOR", 4);
        Store(registry, "BATT_LOW_VOLT", 14.0f);
        Store(registry, "BATT_CRT_VOLT", 13.0f);
        var now = DateTimeOffset.UtcNow;
        var service = CreateService(new TestActiveVehicleContext(State(now, 15, 0)), registry, now);

        // Low must stay above critical (13.0 V); 12.5 V is invalid.
        var rejected = await service.SetValueAsync(vehicleId, 1, BatterySetting.LowVoltage, 12.5, TestContext.Current.CancellationToken);
        rejected.Success.Should().BeFalse();
        registry.GetParameter(vehicleId, "BATT_LOW_VOLT")!.Value.Should().Be(14.0f);

        var accepted = await service.SetValueAsync(vehicleId, 1, BatterySetting.LowVoltage, 15.0, TestContext.Current.CancellationToken);
        accepted.Success.Should().BeTrue();
        registry.GetParameter(vehicleId, "BATT_LOW_VOLT")!.Value.Should().Be(15.0f);
    }

    /// <summary>Verifies a configuration surfaces invalid stored threshold ordering as a blocking issue.</summary>
    [Fact]
    public async Task ConfigurationSurfacesInvalidStoredOrdering()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "BATT_MONITOR", 4);
        Store(registry, "BATT_LOW_VOLT", 12.0f);
        Store(registry, "BATT_CRT_VOLT", 13.0f); // Critical above low is invalid.
        var now = DateTimeOffset.UtcNow;
        var service = CreateService(new TestActiveVehicleContext(State(now, 15, 0)), registry, now);

        var configuration = await service.GetConfigurationAsync(vehicleId, TestContext.Current.CancellationToken);

        configuration.Issues.Should().Contain(issue => issue.Severity == BatteryIssueSeverity.Blocking);
    }

    private static BatteryConfigurationService CreateService(TestActiveVehicleContext context, VehicleParameterRegistry registry, DateTimeOffset now)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now);
        var metadata = Substitute.For<IVehicleParameterMetadataService>();
        metadata.GetAllMetadataAsync(vehicleId, Arg.Any<CancellationToken>()).Returns(new Dictionary<string, ParameterMetadata>());
        metadata.GetMetadataAsync(vehicleId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((ParameterMetadata?)null);
        var parameterService = Substitute.For<IVehicleParameterService>();
        parameterService.SetParameterAsync(vehicleId, Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                registry.StoreParameter(vehicleId, new VehicleParameter(call.ArgAt<string>(1), call.ArgAt<float>(2), MavParamType.Real32, 0, 1), CancellationToken.None);
                return Task.FromResult(true);
            });
        return new BatteryConfigurationService(context, registry, metadata, parameterService, clock,
            Substitute.For<ILogger<BatteryConfigurationService>>());
    }

    private static void Store(VehicleParameterRegistry registry, string name, float value) =>
        registry.StoreParameter(vehicleId, new VehicleParameter(name, value, MavParamType.Real32, 0, 1), CancellationToken.None);

    private static VehicleState State(DateTimeOffset now, double voltage, double current)
    {
        var state = new VehicleState(vehicleId, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
            VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null) with
        {
            Flight = new VehicleFlightState(0, 0, 4, VehicleMode.Stabilize, false,
                LandedState: VehicleLandedState.OnGround, ObservedAt: now)
        };
        return state with
        {
            Power = VehiclePowerState.Empty with
            {
                BatteryVoltageVolts = voltage,
                BatteryCurrentAmps = current,
                BatteryConsumedMah = 100,
                BatteryRemainingPercent = 80,
                ObservedAt = now
            }
        };
    }

    private sealed class TestActiveVehicleContext(VehicleState state) : IActiveVehicleContext
    {
        private readonly CancellationTokenSource lifetime = new();

        public ActiveVehicleSnapshot Current { get; } = new(state.VehicleId, state);

        public VehicleId? VehicleId => Current.VehicleId;

        public VehicleState? State => Current.State;

        public bool IsOnline => Current.IsOnline;

        public CancellationToken ConnectionCancellationToken => lifetime.Token;

        public event EventHandler<ActiveVehicleChangedEventArgs>? Changed;
    }
}
