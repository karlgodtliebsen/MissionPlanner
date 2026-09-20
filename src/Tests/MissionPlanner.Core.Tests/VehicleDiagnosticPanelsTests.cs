using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Tests;

/// <summary>Checks missing-value and configured-role rendering without vehicle-specific assumptions.</summary>
public sealed class VehicleDiagnosticPanelsTests
{
    /// <summary>Only known mappings are emphasized and invalid PWM values stay unavailable.</summary>
    [Fact]
    public void RcUsesConfiguredMappingAndCalibration()
    {
        var state = VehicleLiveDiagnosticsTests.State(new VehicleId(32, 1));
        state = state with { Radio = state.Radio with { ChannelsRaw = new ushort[] { 1500, 1000, ushort.MaxValue }, ChannelCount = 16 } };
        var parameters = new Dictionary<string, float>
        {
            ["RCMAP_ROLL"] = 2, ["RC2_MIN"] = 988, ["RC2_TRIM"] = 1500, ["RC2_MAX"] = 2011
        };
        var rows = VehicleDiagnosticPanels.Rc(state, key => parameters.TryGetValue(key, out var value) ? value : null);
        Assert.Equal(3, rows.Count);
        Assert.False(rows[0].Assigned);
        Assert.Contains("Roll", rows[1].Name);
        Assert.True(rows[1].Assigned);
        Assert.Equal(988, rows[1].Minimum);
        Assert.Null(rows[2].Value);
        Assert.Empty(VehicleDiagnosticPanels.Rc(null, _ => null));
    }

    /// <summary>Reported voltage is shown without chemistry thresholds or manufactured health.</summary>
    [Fact]
    public void PowerKeepsMissingValuesUnknownAndShowsFailsafeEvidence()
    {
        var state = VehicleLiveDiagnosticsTests.State(new VehicleId(9, 1));
        state = state with { Power = state.Power with { BatteryVoltageVolts = 0.797 } };
        var snapshot = new VehicleLiveDiagnosticSnapshot(state.VehicleId, state, "Serial", "COM12", false, 1, DateTimeOffset.UtcNow);
        var arming = new VehicleArmingDiagnostic("DISARMED / NOT READY", false, false, ["Battery failsafe"], null, null, null);
        var rows = VehicleDiagnosticPanels.Describe("Power", snapshot, arming, DateTimeOffset.UtcNow);
        Assert.Contains(rows, row => row.Contains("0.797"));
        Assert.Contains(rows, row => row.Contains("Battery failsafe"));
        Assert.Contains(rows, row => row.Contains("Current: unavailable"));
    }
}
