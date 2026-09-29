using MissionPlanner.Analysis.Frequency;
using MissionPlanner.Analysis.Vibration;

namespace MissionPlanner.Analysis.Tests;

public sealed class FrequencyAnalysisTests
{
    private readonly FftAnalyzer fft = new();
    private readonly PeakDetector detector = new();
    private readonly HarmonicDetector harmonics = new();

    [Theory]
    [InlineData(WindowFunction.Hann)]
    [InlineData(WindowFunction.Rectangular)]
    public void BinCenteredToneHasCorrectAmplitudeAndResolution(WindowFunction window)
    {
        var samples = new SignalBuilder().WithSampleRate(1024).WithDuration(1).AddSine(73, 2.5).Build();
        var spectrum = fft.Analyze(samples, 1024, new FftOptions { Window = window });
        var peak = Assert.Single(detector.Detect(spectrum, new PeakDetectionOptions()));
        Assert.Equal(73, peak.FrequencyHz);
        Assert.Equal(2.5, peak.Amplitude, 10);
        Assert.Equal(1, spectrum.ResolutionHz);
        Assert.Equal(512, spectrum.NyquistHz);
        Assert.Equal(513, spectrum.Bins.Length);
    }

    [Theory]
    [InlineData(73, 1000)]
    [InlineData(163, 1200)]
    public void ReferenceSignalsRetainFundamentalAndHarmonicEvidence(double fundamental, double rate)
    {
        var samples = new SignalBuilder().WithSampleRate(rate).WithDuration(4096 / rate)
            .AddSine(fundamental).AddSine(fundamental * 2, 0.4).AddSine(fundamental * 3, 0.2).Build();
        var spectrum = fft.Analyze(samples, rate, new FftOptions { Size = 4096 });
        var peaks = detector.Detect(spectrum, new PeakDetectionOptions());
        Assert.Equal(3, peaks.Length);
        for (var order = 1; order <= 3; order++)
        {
            Assert.InRange(Math.Abs(peaks[order - 1].FrequencyHz - fundamental * order), 0, spectrum.ResolutionHz / 2);
        }
        var series = Assert.Single(harmonics.Detect(peaks, spectrum.ResolutionHz));
        Assert.InRange(Math.Abs(series.FundamentalHz - fundamental), 0, spectrum.ResolutionHz / 2);
        Assert.Equal(new[] { 1, 2, 3 }, series.Harmonics.Select(h => h.Order));
        Assert.All(series.Harmonics, h => Assert.InRange(Math.Abs(h.DifferenceHz), 0, h.ToleranceHz));
        Assert.InRange(series.Confidence, 0.1, 1);
    }

    [Fact]
    public void NonHarmonicPeaksDoNotBecomeASeries()
    {
        var samples = new SignalBuilder().WithSampleRate(1024).WithDuration(1)
            .AddSine(73).AddSine(151, 0.4).AddSine(251, 0.2).Build();
        var spectrum = fft.Analyze(samples, 1024, new FftOptions());
        var peaks = detector.Detect(spectrum, new PeakDetectionOptions());
        Assert.Equal(3, peaks.Length);
        Assert.Empty(harmonics.Detect(peaks, spectrum.ResolutionHz));
    }

    [Fact]
    public void DcRemovalAndEndpointScalingAreExplicit()
    {
        var samples = Enumerable.Range(0, 1024).Select(i => 3.0 + (i % 2 == 0 ? 2 : -2)).ToArray();
        var options = new FftOptions { Window = WindowFunction.Rectangular, RemoveDc = false };
        var retained = fft.Analyze(samples, 1024, options);
        Assert.Equal(3, retained.Bins[0].Amplitude, 10);
        Assert.Equal(2, retained.Bins[^1].Amplitude, 10);
        Assert.Equal(512, Assert.Single(detector.Detect(retained, new PeakDetectionOptions())).FrequencyHz);
        var removed = fft.Analyze(samples, 1024, options with { RemoveDc = true });
        Assert.Equal(3, removed.RemovedMean, 10);
        Assert.Equal(0, removed.Bins[0].Amplitude, 10);
        Assert.Equal(2, removed.Bins[^1].Amplitude, 10);
    }

    [Theory]
    [InlineData(73.5)]
    [InlineData(510.2)]
    public void OffBinAndNearNyquistTonesStayWithinOneBin(double frequency)
    {
        var samples = new SignalBuilder().WithSampleRate(1024).WithDuration(1).AddSine(frequency).Build();
        var spectrum = fft.Analyze(samples, 1024, new FftOptions());
        var peak = detector.Detect(spectrum, new PeakDetectionOptions()).MaxBy(p => p.Amplitude)!;
        Assert.InRange(Math.Abs(peak.FrequencyHz - frequency), 0, spectrum.ResolutionHz);
        Assert.InRange(peak.Amplitude, 0.8, 1.05);
    }

    [Fact]
    public void SeededNoiseIsRepeatableAndThresholdsSuppressSubNoiseTone()
    {
        var builder = new SignalBuilder().AddSine(73, 0.001).AddNoise(1);
        var samples = builder.Build();
        Assert.Equal(samples, builder.Build());
        var spectrum = fft.Analyze(samples, 1000, new FftOptions());
        Assert.Empty(detector.Detect(spectrum, new PeakDetectionOptions
        {
            MinimumAmplitude = 0, RelativeThreshold = 0, NoiseFloorMultiplier = 8
        }));
    }

    [Fact]
    public void StrongSignalSurvivesNoiseAndDcOffset()
    {
        var samples = new SignalBuilder().WithSampleRate(1024).WithDuration(1)
            .AddSine(73).WithDc(42).AddNoise(0.1).Build();
        var spectrum = fft.Analyze(samples, 1024, new FftOptions());
        var peak = Assert.Single(detector.Detect(spectrum, new PeakDetectionOptions()));
        Assert.Equal(73, peak.FrequencyHz);
        Assert.InRange(peak.Amplitude, 0.98, 1.02);
        Assert.InRange(spectrum.RemovedMean, 41.99, 42.01);
        Assert.True(peak.Amplitude > peak.Threshold);
        Assert.True(peak.NoiseFloor > 0);
    }

    [Fact]
    public void ZeroSignalHasNoPeaksOrAssessments()
    {
        var analyzer = new VibrationAnalyzer(fft, detector, harmonics);
        var result = analyzer.Analyze(new double[1024], 1000, new VibrationSource("IMU1", VibrationAxis.X, "rad/s"),
            new FftOptions(), new PeakDetectionOptions());
        Assert.Empty(result.Peaks);
        Assert.Empty(result.Assessments);
    }

    [Fact]
    public void VibrationInterpretationRetainsRawEvidenceWithoutDiagnosingACause()
    {
        var samples = new SignalBuilder().WithSampleRate(1024).WithDuration(1)
            .AddSine(73).AddSine(146, 0.4).AddSine(219, 0.2).Build();
        var analyzer = new VibrationAnalyzer(fft, detector, harmonics);
        var result = analyzer.Analyze(samples, 1024, new VibrationSource("IMU1", VibrationAxis.Z, "m/s²"),
            new FftOptions(), new PeakDetectionOptions());
        var assessment = Assert.Single(result.Assessments);
        Assert.Same(result.Harmonics[0], assessment.Series);
        Assert.Contains("candidate", assessment.Description);
        Assert.Contains("undetermined", assessment.Limitation);
        Assert.All(assessment.Series.Harmonics, h => Assert.Contains(h.Peak, result.Peaks));
        Assert.Equal(513, result.Spectrum.Bins.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(1000)]
    public void UnsupportedTransformSizesAreRejected(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => fft.Analyze(new double[size], 1000, new FftOptions { Size = size }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(1025)]
    public void InputIsNeverImplicitlyPaddedOrTruncated(int count)
    {
        Assert.Throws<ArgumentException>(() => fft.Analyze(new double[count], 1000, new FftOptions()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidSampleRatesAreRejected(double rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => fft.Analyze(new double[1024], rate, new FftOptions()));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteSamplesAreRejected(double value)
    {
        var samples = new double[1024];
        samples[1] = value;
        Assert.Throws<ArgumentException>(() => fft.Analyze(samples, 1000, new FftOptions()));
    }

    [Theory]
    [InlineData(WindowFunction.Hann)]
    [InlineData(WindowFunction.Rectangular)]
    public void FftMatchesIndependentDirectTransform(WindowFunction window)
    {
        const int count = 16;
        var samples = new SignalBuilder().WithSampleRate(16).WithDuration(1).AddNoise(1).Build();
        var spectrum = fft.Analyze(samples, 16, new FftOptions { Size = count, Window = window, RemoveDc = false });
        var weights = Enumerable.Range(0, count).Select(i => window == WindowFunction.Hann
            ? 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / count) : 1).ToArray();
        for (var bin = 0; bin <= count / 2; bin++)
        {
            var real = 0.0;
            var imaginary = 0.0;
            for (var i = 0; i < count; i++)
            {
                real += samples[i] * weights[i] * Math.Cos(2 * Math.PI * bin * i / count);
                imaginary -= samples[i] * weights[i] * Math.Sin(2 * Math.PI * bin * i / count);
            }
            var expected = Math.Sqrt(real * real + imaginary * imaginary) / weights.Sum()
                * (bin == 0 || bin == count / 2 ? 1 : 2);
            Assert.Equal(expected, spectrum.Bins[bin].Amplitude, 12);
        }
    }

    [Fact]
    public void RelativeAndAbsoluteThresholdsSelectOnlyStrongTones()
    {
        var samples = new SignalBuilder().WithSampleRate(1024).WithDuration(1).AddSine(73).AddSine(171, 0.1).Build();
        var spectrum = fft.Analyze(samples, 1024, new FftOptions());
        Assert.Equal(2, detector.Detect(spectrum, new PeakDetectionOptions()).Length);
        Assert.Single(detector.Detect(spectrum, new PeakDetectionOptions { RelativeThreshold = 0.2 }));
        Assert.Single(detector.Detect(spectrum, new PeakDetectionOptions { MinimumAmplitude = 0.2 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => detector.Detect(spectrum, new PeakDetectionOptions { RelativeThreshold = double.NaN }));
    }
}
