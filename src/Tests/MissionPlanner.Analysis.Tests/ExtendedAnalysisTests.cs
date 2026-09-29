using System.Collections.Immutable;
using System.Text.Json;
using MissionPlanner.Analysis.Frequency;
using MissionPlanner.Analysis.Vibration;

namespace MissionPlanner.Analysis.Tests;

public sealed class ExtendedAnalysisTests
{
    [Fact]
    public void CorrelationPreservesMeasuredPairsAndFrequencyResiduals()
    {
        var pairs = Enumerable.Range(0, 6).Select(i => new MotorFrequencyObservation(i, (80 + i * 30) * 60, 82 + i * 30, 1)).ToImmutableArray();
        var result = new MotorFrequencyCorrelator().Analyze(2, "ESC RPM", pairs);
        Assert.Equal(1, result.Correlation!.Value, 10);
        Assert.Equal(2, result.DifferenceHz, 10);
        Assert.Equal(2, result.RootMeanSquareErrorHz, 10);
        Assert.Equal(pairs, result.Evidence);
    }

    [Fact]
    public void ConstantSpeedCannotEstablishCorrelation()
    {
        var pairs = Enumerable.Range(0, 5).Select(i => new MotorFrequencyObservation(i, 6000, 100, 1)).ToImmutableArray();
        var result = new MotorFrequencyCorrelator().Analyze(0, "RPM sensor", pairs);
        Assert.Null(result.Correlation);
        Assert.Null(new ResonanceDetector().Detect(result, 10));
    }

    [Fact]
    public void SweepWithLocalizedAmplificationProducesQualifiedCandidate()
    {
        var pairs = new[] { 80, 130, 175, 180, 185, 230, 260 }
            .Select((hz, i) => new MotorFrequencyObservation(i, hz * 60, hz, hz == 180 ? 8 : 1)).ToImmutableArray();
        var correlation = new MotorFrequencyCorrelator().Analyze(0, "ESC", pairs);
        var candidate = new ResonanceDetector().Detect(correlation, 10);
        Assert.NotNull(candidate);
        Assert.Equal(175, candidate.MinimumFrequencyHz);
        Assert.Equal(185, candidate.MaximumFrequencyHz);
        Assert.Equal(8, candidate.RelativeAmplification);
        Assert.Equal(pairs, candidate.Evidence);
        Assert.Null(new ResonanceDetector().Detect(correlation with
        {
            Evidence = pairs.Select(p => p with { Amplitude = 1 }).ToImmutableArray()
        }, 10));
    }

    [Fact]
    public void EndOfSweepAmplificationHasNoTwoSidedResonanceEvidence()
    {
        var pairs = Enumerable.Range(0, 6).Select(i => new MotorFrequencyObservation(i, (80 + i * 30) * 60,
            80 + i * 30, i == 5 ? 10 : 1)).ToImmutableArray();
        Assert.Null(new ResonanceDetector().Detect(new MotorFrequencyCorrelator().Analyze(0, "ESC", pairs), 10));
    }

    [Fact]
    public void DifferentResolutionsComparePhysicalFrequencyRatherThanBinIndex()
    {
        var fft = new FftAnalyzer();
        var a = new SignalBuilder().WithSampleRate(1024).WithDuration(1).AddSine(128).Build();
        var b = new SignalBuilder().WithSampleRate(1024).WithDuration(0.5).AddSine(128, 2).Build();
        var left = fft.Analyze(a, 1024, new FftOptions());
        var right = fft.Analyze(b, 1024, new FftOptions { Size = 512 });
        var differences = new SpectrumComparer().Compare(left, right);
        var row = Assert.Single(differences.Where(d => d.FrequencyHz == 128));
        Assert.Equal(1, row.Baseline, 10);
        Assert.Equal(2, row.Current, 10);
        Assert.Equal(1, row.Change, 10);
        Assert.Equal(257, differences.Length);
    }

    [Fact]
    public void ComparisonRejectsIncompatibleWindowSemantics()
    {
        var fft = new FftAnalyzer();
        var samples = new double[1024];
        Assert.Throws<ArgumentException>(() => new SpectrumComparer().Compare(
            fft.Analyze(samples, 1024, new FftOptions()),
            fft.Analyze(samples, 1024, new FftOptions { Window = WindowFunction.Rectangular })));
    }

    [Fact]
    public void NotchPredictionHasSpecifiedCenterAttenuationAndRetainsMeasurements()
    {
        var samples = new SignalBuilder().WithSampleRate(1024).WithDuration(1).AddSine(128).Build();
        var spectrum = new FftAnalyzer().Analyze(samples, 1024, new FftOptions());
        var simulator = new NotchFilterSimulator();
        var simulation = simulator.Simulate(spectrum, [new NotchFilter(128, 20, 40)]);
        Assert.Equal(1, simulation[128].MeasuredAmplitude, 10);
        Assert.Equal(0.01, simulation[128].Gain, 8);
        Assert.Equal(0.01, simulation[128].SimulatedAmplitude, 8);
        Assert.Equal(1, simulation[0].Gain, 10);
        Assert.Equal(1, simulation[^1].Gain, 10);
        Assert.Throws<ArgumentException>(() => simulator.Simulate(spectrum, [new NotchFilter(512, 10, 30)]));
    }

    [Fact]
    public void AverageUsesEveryCompleteWindowAndRetainsScaling()
    {
        var samples = new SignalBuilder().WithSampleRate(1024).WithDuration(2).AddSine(128, 3).Build();
        var result = new SpectrumAverager(new FftAnalyzer()).Analyze(samples, 1024, new FftOptions(), TestContext.Current.CancellationToken);
        Assert.Equal(3, result.Bins[128].Amplitude, 10);
    }

    [Fact]
    public void SyntheticRegressionManifestsExecuteExpectedFrequencyChecks()
    {
        var paths = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "TestData", "Frequency", "Synthetic"), "*.json");
        Assert.NotEmpty(paths);
        foreach (var path in paths)
        {
            using var metadata = JsonDocument.Parse(File.ReadAllText(path));
            var root = metadata.RootElement;
            var rate = root.GetProperty("sampleRateHz").GetDouble();
            var count = root.GetProperty("sampleCount").GetInt32();
            var builder = new SignalBuilder().WithSampleRate(rate).WithDuration(count / rate);
            foreach (var tone in root.GetProperty("tones").EnumerateArray())
            {
                builder.AddSine(tone.GetProperty("frequencyHz").GetDouble(), tone.GetProperty("amplitude").GetDouble());
            }
            var spectrum = new FftAnalyzer().Analyze(builder.Build(), rate, new FftOptions { Size = count });
            var peaks = new PeakDetector().Detect(spectrum, new PeakDetectionOptions());
            var series = new HarmonicDetector().Detect(peaks, spectrum.ResolutionHz);
            var expected = root.GetProperty("expected").GetProperty("fundamentalHz").GetDouble();
            Assert.Contains(series, s => Math.Abs(s.FundamentalHz - expected) <= spectrum.ResolutionHz);
            foreach (var harmonic in root.GetProperty("expected").GetProperty("harmonicsHz").EnumerateArray())
            {
                Assert.Contains(peaks, p => Math.Abs(p.FrequencyHz - harmonic.GetDouble()) <= spectrum.ResolutionHz);
            }
        }
    }
}
