using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies hardware discovery and passive telemetry do not fabricate support or fresh values.</summary>
public sealed class SetupContextTests
{
    /// <summary>Both GPS parameter generations expose detected nodes as read-only assignment choices.</summary>
    [Theory]
    [InlineData("GPS_CAN_NODEID1", "GPS_CAN_NODEID2")]
    [InlineData("GPS1_CAN_NODEID", "GPS2_CAN_NODEID")]
    public void CanGpsAliasesOfferDetectedNodesAndRetainMissingOverride(string first, string second)
    {
        var values = new Dictionary<string, VehicleParameter>();
        foreach (var (name, value) in new[] { (first, 21), (second, 42), ("GPS1_CAN_OVRIDE", 99), ("GPS2_CAN_OVRIDE", 0) })
            values[name] = new(name, value, MavParamType.Int32, 0, 4);
        var view = new CanGpsOrderModule().Build(values, new Dictionary<string, ParameterMetadata>());
        Assert.True(view.Settings.Single(setting => setting.Name == first).ReadOnly);
        Assert.True(view.Settings.Single(setting => setting.Name == second).ReadOnly);
        var assigned = view.Settings.Single(setting => setting.Name == "GPS1_CAN_OVRIDE");
        Assert.Equal(99, assigned.CurrentValue);
        Assert.Equal(new[] { 0d, 21d, 42d }, assigned.Options.Select(option => option.Value));
        Assert.False(assigned.ReadOnly);
    }

    /// <summary>Helicopter settings require the actual swash/rotor parameters and preserve only reported fields.</summary>
    [Fact]
    public void HelicopterModuleOnlyOffersFirmwareReportedSettings()
    {
        var module = new TraditionalHelicopterModule();
        var values = new Dictionary<string, VehicleParameter>();
        Assert.False(module.IsAvailable(values));
        foreach (var name in new[] { "H_SW_TYPE", "H_RSC_MODE", "H2_COL_MAX", "IM_STB_COL_1", "ATC_RAT_RLL_P" })
            values[name] = new(name, 0, MavParamType.Int32, 0, 5);
        Assert.True(module.IsAvailable(values));
        var view = module.Build(values, new Dictionary<string, ParameterMetadata>());
        Assert.Equal(4, view.Settings.Count);
        Assert.DoesNotContain(view.Settings, setting => setting.Name == "ATC_RAT_RLL_P");
    }

    /// <summary>Range sensors remain individually identified and stale/future readings cannot look live.</summary>
    [Fact]
    public void SetupContextKeepsFreshnessAndSensorIdentity()
    {
        var now = DateTimeOffset.UtcNow;
        var state = new VehicleState(new(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
            VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null);
        state = state with { Range = new(new Dictionary<byte, VehicleRangeSensorState>
        {
            [7] = new(7, 2.5, .1, 10, 25, 90, now),
            [8] = new(8, 8.75, .1, 10, 0, 95, now.AddSeconds(-4)),
            [9] = new(9, 9.5, .1, 10, 0, 99, now.AddSeconds(1))
        }), Power = state.Power with { BatteryVoltageVolts = 12.5, BatteryRemainingPercent = 80, ObservedAt = now } };
        var text = SetupTelemetrySummary.Rangefinders(state, now);
        Assert.Contains("Sensor 7, orientation 25", text);
        Assert.Contains("Sensor 8, orientation 0: unavailable / stale", text);
        Assert.Contains("Sensor 9, orientation 0: unavailable / stale", text);
        Assert.DoesNotContain("RNGFND7", text);
        Assert.Contains("80%", SetupTelemetrySummary.Failsafe(state, now));
        Assert.Contains("Battery unavailable / stale", SetupTelemetrySummary.Failsafe(state, now.AddSeconds(4)));
    }
}
