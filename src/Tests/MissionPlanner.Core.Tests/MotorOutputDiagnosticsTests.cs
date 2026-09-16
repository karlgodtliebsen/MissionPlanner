using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies diagnostic mapping and evidence-based motor guidance.</summary>
public sealed class MotorOutputDiagnosticsTests
{
    /// <summary>Shows nonsequential Motor1-4 assignments, frame positions and protocol together.</summary>
    [Fact]
    public void SummaryUsesActualOutputAssignments()
    {
        var id = new VehicleId(1, 1);
        var registry = new VehicleParameterRegistry();
        var values = new Dictionary<string, float>
        {
            ["FRAME_CLASS"] = 1, ["FRAME_TYPE"] = 1, ["MOT_PWM_TYPE"] = 6,
            ["SERVO1_FUNCTION"] = 34, ["SERVO2_FUNCTION"] = 35,
            ["SERVO3_FUNCTION"] = 36, ["SERVO4_FUNCTION"] = 33, ["RC8_OPTION"] = 32
        };
        foreach (var pair in values)
        {
            registry.StoreParameter(id, new VehicleParameter(pair.Key, pair.Value, MavParamType.Real32, 0, 1),
                TestContext.Current.CancellationToken);
        }
        var parameters = registry.GetAllParameters(id);
        var layout = new MotorLayoutResolver().Resolve(parameters);
        var resolver = new MotorOutputResolver(registry);
        var summary = MotorOutputDiagnostics.Describe(parameters, layout, number => resolver.Resolve(id, number));
        Assert.Contains("Motor 1", summary);
        Assert.Contains("front/right", summary);
        Assert.Contains("output 4 (Resolved)", summary);
        Assert.Contains("output 1 (Resolved)", summary);
        Assert.Contains("output 2 (Resolved)", summary);
        Assert.Contains("output 3 (Resolved)", summary);
        Assert.Contains("DShot600", summary);
        Assert.Contains("RC8_OPTION = 32", summary);
        Assert.Contains("MOT_SAFE_DISARM = unavailable", summary);
        Assert.Contains("Timer/output groups: unavailable", summary);
    }

    /// <summary>Command failures never assert that every physical motor failed.</summary>
    [Fact]
    public void GuidanceSeparatesAcknowledgementFromRotation()
    {
        var unknown = MotorOutputDiagnostics.Guidance(0, 4, "Denied");
        Assert.Contains("does not prove movement", unknown);
        Assert.Contains("does not establish failure of every motor", unknown);
        Assert.Contains("If the vehicle arms", unknown);
        Assert.Contains("protocol, output mapping and ESC power", unknown);
        var observed = MotorOutputDiagnostics.Guidance(4, 4, null);
        Assert.Contains("User confirmed rotation on 4/4", observed);
        Assert.Contains("paths operated during the test", observed);
    }
}
