using MissionPlanner.Core.Setup.OptionalHardware;
namespace MissionPlanner.Core.Tests;
public sealed class JoystickControlTests
{
    [Theory]
    [InlineData(0.02, false, 0)]
    [InlineData(0.5, false, 0.5)]
    [InlineData(0.5, true, -0.5)]
    [InlineData(2, false, 1)]
    public void CalibrationAppliesDeadzoneReverseAndBounds(double raw, bool reverse, double expected)
    {
        var mapping = new JoystickAxisMapping(JoystickFunction.Roll, 0, -1, 0, 1, .05, reverse);
        Assert.Equal(expected, JoystickCalibration.Normalize(raw, mapping), 6);
    }

    [Fact]
    public async Task UnsupportedProviderIsExplicitAndEnumeratesNothing()
    {
        var provider = new UnsupportedJoystickProvider();
        Assert.False(provider.IsSupported);
        Assert.Empty(await provider.EnumerateAsync(TestContext.Current.CancellationToken));
    }
}
