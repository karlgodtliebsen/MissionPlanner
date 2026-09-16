using MissionPlanner.Core.Setup.MandatoryHardware;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies compass intent is not confused with detected hardware or absent data.</summary>
public sealed class CompassDiagnosticsTests
{
    /// <summary>Required absent hardware fails, healthy detected hardware passes, and compassless configuration is valid.</summary>
    [Theory]
    [InlineData(1, 1, 197121, true, true)]
    [InlineData(1, 1, 0, false, false)]
    [InlineData(0, 8, 0, false, true)]
    public void ConfigurationAndDetectionAreIndependent(int enabled, int yawSource, int deviceId, bool detected, bool healthy)
    {
        var values = Values(enabled, yawSource, deviceId);
        var result = CompassDiagnostics.Evaluate(values, true);
        Assert.Equal(detected, result.Detected);
        Assert.Equal(healthy, result.Healthy);
        Assert.Equal(yawSource == 1, result.Required);
        Assert.Contains(result.Evidence, text => text.Contains($"COMPASS_DEV_ID={deviceId}"));
    }

    /// <summary>Missing downloaded parameters remain unknown rather than asserting a healthy compassless system.</summary>
    [Fact]
    public void MissingParametersRemainUnknown()
    {
        var result = CompassDiagnostics.Evaluate(new Dictionary<string, float>(), null);
        Assert.Null(result.Configured);
        Assert.Null(result.Detected);
        Assert.Null(result.Required);
        Assert.Null(result.Healthy);
    }

    /// <summary>Raw device identity decodes the documented 3/5/8/8-bit layout.</summary>
    [Fact]
    public void DeviceIdentityKeepsRawAndDecodedFields()
    {
        var id = 1u | (2u << 3) | (30u << 8) | (42u << 16);
        var result = CompassDiagnostics.DecodeDeviceId(id);
        Assert.Contains("I2C #2", result);
        Assert.Contains("Address 30", result);
        Assert.Contains("Device type 42", result);
    }

    /// <summary>A detected but disabled second compass cannot satisfy a missing configured primary.</summary>
    [Fact]
    public void UnusedDetectedDeviceDoesNotHideMissingRequiredSensor()
    {
        var values = Values(1, 1, 0);
        values["COMPASS_DEV_ID2"] = 197121;
        Assert.False(CompassDiagnostics.Evaluate(values, true).Healthy);
    }

    private static Dictionary<string, float> Values(int enabled, int yawSource, int deviceId) => new()
    {
        ["COMPASS_ENABLE"] = enabled,
        ["COMPASS_USE"] = enabled,
        ["COMPASS_USE2"] = 0,
        ["COMPASS_USE3"] = 0,
        ["COMPASS_DEV_ID"] = deviceId,
        ["COMPASS_DEV_ID2"] = 0,
        ["COMPASS_DEV_ID3"] = 0,
        ["EK3_SRC1_YAW"] = yawSource,
        ["EK3_SRC2_YAW"] = 0,
        ["EK3_SRC3_YAW"] = 0
    };
}
