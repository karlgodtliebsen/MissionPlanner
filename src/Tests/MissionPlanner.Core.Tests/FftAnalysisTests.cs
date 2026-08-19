using MissionPlanner.Core.Setup.OptionalHardware;
namespace MissionPlanner.Core.Tests;
public sealed class FftAnalysisTests
{
    [Theory]
    [InlineData(50)]
    [InlineData(123)]
    public void SyntheticSineProducesExpectedPeak(double expectedHz)
    {
        const int count = 1000; const double rate = 1000;
        var samples = Enumerable.Range(0, count).Select(i => Math.Sin(2 * Math.PI * expectedHz * i / rate)).ToArray();
        var result = new FftAnalysisService().Analyze(samples, rate);
        Assert.InRange(result.Peak.FrequencyHz, expectedHz - 1, expectedHz + 1);
    }
}
