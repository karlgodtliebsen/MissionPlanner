using MissionPlanner.Analysis.Frequency;

namespace MissionPlanner.Analysis.Tests;

public sealed class SpectrogramTests
{
    private readonly SpectrogramAnalyzer analyzer = new(new FftAnalyzer());

    [Fact]
    public void SweepDominantFrequencyRisesWithExpectedTiming()
    {
        const int count = 4096;
        const double rate = 1024;
        var samples = SignalBuilder.Sweep(count, rate, 40, 240);
        var result = analyzer.Analyze(samples, rate, new SpectrogramOptions
        {
            Fft = new FftOptions { Size = 256 }, OverlapSamples = 128,
            MinimumFrequencyHz = 20, MaximumFrequencyHz = 300
        }, TestContext.Current.CancellationToken);
        Assert.Equal(31, result.Frames.Length);
        Assert.Equal(0, result.UnusedTailSamples);
        var previous = 0.0;
        foreach (var frame in result.Frames)
        {
            var peak = frame.Bins.MaxBy(b => b.Amplitude)!;
            var expected = 40 + 200 * frame.CenterTimeSeconds / ((count - 1) / rate);
            Assert.InRange(Math.Abs(peak.FrequencyHz - expected), 0, rate / 256);
            Assert.True(peak.FrequencyHz > previous);
            Assert.Equal((frame.StartSample + 127.5) / rate, frame.CenterTimeSeconds);
            Assert.All(frame.Bins, b => Assert.InRange(b.FrequencyHz, 20, 300));
            previous = peak.FrequencyHz;
        }
    }

    [Fact]
    public void PartialTailIsReportedAndNotPadded()
    {
        var result = analyzer.Analyze(new double[1100], 1000, new SpectrogramOptions(), TestContext.Current.CancellationToken);
        Assert.Single(result.Frames);
        Assert.Equal(76, result.UnusedTailSamples);
        Assert.Equal(1100, result.InputSampleCount);
    }

    [Theory]
    [InlineData(-1, 0, 500)]
    [InlineData(1024, 0, 500)]
    [InlineData(512, -1, 500)]
    [InlineData(512, 100, 90)]
    [InlineData(512, 0, 501)]
    [InlineData(512, double.NaN, 500)]
    public void InvalidOverlapAndFrequencyRangesAreRejected(int overlap, double min, double max)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => analyzer.Analyze(new double[1024], 1000,
            new SpectrogramOptions { OverlapSamples = overlap, MinimumFrequencyHz = min, MaximumFrequencyHz = max }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void InsufficientSamplesEmptyBinRangesAndInvalidTailAreRejected()
    {
        Assert.Throws<ArgumentException>(() => analyzer.Analyze(new double[100], 1000, new SpectrogramOptions(), TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => analyzer.Analyze(new double[1024], 1024,
            new SpectrogramOptions { MinimumFrequencyHz = 0.2, MaximumFrequencyHz = 0.3 }, TestContext.Current.CancellationToken));
        var samples = new double[1025];
        samples[^1] = double.NaN;
        Assert.Throws<ArgumentException>(() => analyzer.Analyze(samples, 1000, new SpectrogramOptions(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AnalysisHonorsCancellation()
    {
        Assert.Throws<OperationCanceledException>(() => analyzer.Analyze(new double[1024], 1000,
            new SpectrogramOptions(), new CancellationToken(true)));
    }
}
