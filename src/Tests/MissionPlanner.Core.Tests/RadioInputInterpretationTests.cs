using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Firmware;

namespace MissionPlanner.Core.Tests;

public sealed class RadioInputInterpretationTests
{
    [Theory]
    [InlineData(2011, 0, 1, "Nose up")]
    [InlineData(2011, 1, -1, "Nose down")]
    [InlineData(988, 0, -1, "Nose down")]
    [InlineData(988, 1, 1, "Nose up")]
    [InlineData(1510, 0, 0, "Neutral")]
    [InlineData(1500, 1, 0, "Neutral")]
    public void PitchUsesRawCalibrationAndReversal(int raw, int reversal, double expected, string label)
    {
        var value = RadioInputInterpretation.Calculate(FirmwareFamily.ArduCopter, "Pitch", raw, 988, 1500, 2011, 20, reversal, true);
        Assert.Equal(expected, value.Normalized);
        Assert.Contains(label, value.Description);
        Assert.Contains("not an FC-reported", value.Description);
    }

    [Fact]
    public void AsymmetricEndpointsAndMissingEvidenceRemainDistinctFromZero()
    {
        var positive = RadioInputInterpretation.Calculate(FirmwareFamily.ArduCopter, "Roll", 1770, 900, 1500, 2020, 20, 0, true);
        Assert.Equal(0.5, positive.Normalized);
        Assert.Contains("Roll right", positive.Description);
        var negative = RadioInputInterpretation.Calculate(FirmwareFamily.ArduCopter, "Roll", 1190, 900, 1500, 2020, 20, 0, true);
        Assert.Equal(-0.5, negative.Normalized);
        Assert.Null(RadioInputInterpretation.Calculate(FirmwareFamily.ArduCopter, "Pitch", 2000, 1000, 1500, 2000, null, 0, true).Normalized);
        Assert.NotNull(RadioInputInterpretation.Calculate(FirmwareFamily.ArduCopter, "Pitch", 2000, 1000, 1500, 2000, 0, 0, true).Normalized);
        Assert.Null(RadioInputInterpretation.Calculate(FirmwareFamily.ArduCopter, "Pitch", 2000, 1000, 1500, 2000, 0, null, true).Normalized);
        Assert.Null(RadioInputInterpretation.Calculate(FirmwareFamily.ArduPlane, "Pitch", 2000, 1000, 1500, 2000, 0, 0, true).Normalized);
        Assert.Null(RadioInputInterpretation.Calculate(FirmwareFamily.ArduCopter, "Pitch", 2000, 1000, 1500, 2000, 0, 0, false).Normalized);
    }
}
