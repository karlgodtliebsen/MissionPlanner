using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MissionPlanner.Analysis.Frequency;
using MissionPlanner.Analysis.Vibration;

namespace MissionPlanner.Analysis.Tests;

/// <summary>Regression checks against the supplied, independently generated CSV signals.</summary>
public sealed class SuppliedFftDatasetTests
{
    private static readonly string FixtureRoot = Path.Combine(AppContext.BaseDirectory, "TestData", "SuppliedFft");

    /// <summary>Checks fixture content integrity, allowing checkout newline conversion, and the sampling contract.</summary>
    [Fact]
    public void ManifestAndChecksumsDescribeAllSixteenUsableDatasets()
    {
        foreach (var line in File.ReadLines(Path.Combine(FixtureRoot, "SHA256SUMS.txt")))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var content = File.ReadAllText(Path.Combine(FixtureRoot, parts[1])).Replace("\r\n", "\n");
            // The supplied hashes use CRLF for CSV, LF for JSON/Markdown. Git may convert either.
            if (parts[1].EndsWith(".csv", StringComparison.Ordinal))
            {
                content = content.Replace("\n", "\r\n");
            }
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
            Assert.True(parts[0] == hash, $"Fixture content checksum: {parts[1]}");
        }
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(FixtureRoot, "manifest.json")));
        var datasets = manifest.RootElement.GetProperty("datasets").EnumerateArray().ToArray();
        Assert.Equal(16, datasets.Length);
        foreach (var entry in datasets)
        {
            var data = Load(entry.GetProperty("dataset").GetString()!);
            Assert.Equal(data.Number("sampleCount"), data.Columns["time_s"].Length);
            Assert.Equal(data.Number("durationSeconds"), data.Columns["time_s"].Length / data.Rate, 9);
            for (var i = 0; i < data.Columns["time_s"].Length; i++)
            {
                Assert.Equal(i / data.Rate, data.Columns["time_s"][i], 6);
            }
            foreach (var channel in data.Expected.GetProperty("channels").EnumerateArray())
            {
                Assert.True(data.Columns.ContainsKey(channel.GetString()!));
            }
        }
    }

    /// <summary>Checks declared stationary tones, including the unrelated and imperfect controls.</summary>
    [Theory]
    [InlineData("01-SingleTone73")]
    [InlineData("02-Harmonics73")]
    [InlineData("03-NoisyHarmonics73")]
    [InlineData("04-DcOffset73")]
    [InlineData("05-BetweenBins73_35")]
    [InlineData("06-NearNyquist470")]
    [InlineData("07-NegativeNonHarmonic")]
    [InlineData("08-NearHarmonicImperfect")]
    public void StationaryPeaksMatchFixtureMetadata(string name)
    {
        var data = Load(name);
        var spectrum = Spectrum(data);
        var peaks = Peaks(spectrum);
        if (data.Expected.TryGetProperty("nyquistHz", out var nyquist))
        {
            Assert.Equal(nyquist.GetDouble(), spectrum.NyquistHz);
            Assert.Equal(nyquist.GetDouble(), spectrum.Bins[^1].FrequencyHz);
        }
        var tolerance = data.Expected.TryGetProperty("frequencyToleranceHz", out var value)
            ? value.GetDouble() : spectrum.ResolutionHz;
        foreach (var frequency in ExpectedTones(data.Expected))
        {
            Assert.Contains(peaks, p => Math.Abs(p.FrequencyHz - frequency) <= tolerance);
        }
        if (data.Expected.TryGetProperty("expectedHarmonicSeries", out var absent))
        {
            Assert.Empty(absent.EnumerateArray());
            Assert.Empty(new HarmonicDetector().Detect(peaks, spectrum.ResolutionHz));
        }
        if (data.Expected.TryGetProperty("expectedFundamentalHz", out var fundamental))
        {
            var series = Assert.Single(new HarmonicDetector().Detect(peaks, spectrum.ResolutionHz));
            Assert.InRange(Math.Abs(series.FundamentalHz - fundamental.GetDouble()), 0, tolerance);
            Assert.Equal(new[] { 1, 2, 3 }, series.Harmonics.Select(h => h.Order));
        }
    }

    /// <summary>Verifies offset removal preserves the measured tone and suppresses DC.</summary>
    [Fact]
    public void DcRemovalPreservesToneAmplitude()
    {
        var data = Load("04-DcOffset73");
        var removed = Spectrum(data);
        var retained = new SpectrumAverager(new FftAnalyzer()).Analyze(data.Columns["gyro_x"], data.Rate,
            new FftOptions { Size = 4096, RemoveDc = false }, TestContext.Current.CancellationToken);
        Assert.InRange(Math.Abs(removed.RemovedMean - data.Number("dcOffset")), 0, 0.002);
        Assert.InRange(removed.Bins[0].Amplitude, 0, 0.002);
        Assert.InRange(Math.Abs(retained.Bins[0].Amplitude - data.Number("dcOffset")), 0, 0.002);
        Assert.Equal(Amplitude(retained, 73), Amplitude(removed, 73), 8);
    }

    /// <summary>Checks two sources without promoting a two-member family to a three-member detection.</summary>
    [Fact]
    public void IndependentSourcesRetainAllPeaksButRequireThreeHarmonicMembers()
    {
        var data = Load("11-TwoHarmonicSources");
        var spectrum = Spectrum(data);
        var peaks = Peaks(spectrum);
        var series = new HarmonicDetector().Detect(peaks, spectrum.ResolutionHz);
        foreach (var family in data.Expected.GetProperty("expectedFamilies").EnumerateArray())
        {
            var fundamental = family.GetProperty("fundamentalHz").GetDouble();
            var harmonics = family.GetProperty("harmonicsHz").EnumerateArray().Select(v => v.GetDouble()).ToArray();
            foreach (var frequency in harmonics.Prepend(fundamental))
            {
                Assert.Contains(peaks, p => Math.Abs(p.FrequencyHz - frequency) <= data.Number("frequencyToleranceHz"));
            }
            Assert.Equal(harmonics.Length >= 2,
                series.Any(s => Math.Abs(s.FundamentalHz - fundamental) <= spectrum.ResolutionHz));
        }
    }

    /// <summary>Checks every complete frame against the independently specified linear sweep.</summary>
    [Theory]
    [InlineData("09-FrequencySweep50-300")]
    [InlineData("10-Resonance180")]
    public void SpectrogramRidgeTracksDeclaredSweep(string name)
    {
        var data = Load(name);
        var sweep = data.Expected.GetProperty("expectedSweep");
        var previous = 0.0;
        foreach (var frame in Spectrogram(data).Frames)
        {
            var peak = frame.Bins.MaxBy(b => b.Amplitude)!;
            var expected = sweep.GetProperty("startHz").GetDouble()
                + (sweep.GetProperty("endHz").GetDouble() - sweep.GetProperty("startHz").GetDouble())
                * frame.CenterTimeSeconds / data.Number("durationSeconds");
            Assert.InRange(Math.Abs(peak.FrequencyHz - expected), 0, data.Rate / 512);
            Assert.True(peak.FrequencyHz >= previous);
            previous = peak.FrequencyHz;
        }
    }

    /// <summary>Uses CSV RPM measurements and independently extracted FFT peaks for correlation.</summary>
    [Fact]
    public void ChangingRpmTracksObservedFrequency()
    {
        var data = Load("13-ChangingRpm80-240");
        var pairs = Spectrogram(data).Frames.Select(frame =>
        {
            var peak = frame.Bins.MaxBy(b => b.Amplitude)!;
            var index = frame.StartSample + 256;
            var rpm = data.Columns["motor1_rpm"][index];
            Assert.Equal(data.Columns["motor1_rotation_hz"][index], rpm / 60, 7);
            return new MotorFrequencyObservation(frame.CenterTimeSeconds, rpm, peak.FrequencyHz, peak.Amplitude);
        }).ToImmutableArray();
        var result = new MotorFrequencyCorrelator().Analyze(1, "Synthetic CSV RPM", pairs);
        Assert.InRange(result.Correlation!.Value, 0.999, 1);
        Assert.InRange(result.RootMeanSquareErrorHz, 0, data.Rate / 512);
    }

    /// <summary>Checks localized amplification using the known synthetic excitation, not invented measured RPM.</summary>
    [Fact]
    public void ResonanceCandidateMatchesInjectedBand()
    {
        var data = Load("10-Resonance180");
        var sweep = data.Expected.GetProperty("expectedSweep");
        var pairs = Spectrogram(data).Frames.Select(frame =>
        {
            var peak = frame.Bins.MaxBy(b => b.Amplitude)!;
            var excitation = sweep.GetProperty("startHz").GetDouble()
                + (sweep.GetProperty("endHz").GetDouble() - sweep.GetProperty("startHz").GetDouble())
                * frame.CenterTimeSeconds / data.Number("durationSeconds");
            return new MotorFrequencyObservation(frame.CenterTimeSeconds, excitation * 60, peak.FrequencyHz, peak.Amplitude);
        }).ToImmutableArray();
        var expected = data.Expected.GetProperty("expectedResonance");
        var band = expected.GetProperty("approxRangeHz").EnumerateArray().Select(v => v.GetDouble()).ToArray();
        var result = new ResonanceDetector().Detect(
            new MotorFrequencyCorrelator().Analyze(0, "Known synthetic excitation", pairs), band[1] - band[0]);
        Assert.NotNull(result);
        Assert.InRange(Math.Abs(result.PeakFrequencyHz - expected.GetProperty("centerHz").GetDouble()),
            0, data.Number("frequencyToleranceHz"));
        Assert.InRange(result.PeakFrequencyHz, band[0], band[1]);
        Assert.True(result.RelativeAmplification >= 3);
    }

    /// <summary>Requires both injected frequencies to appear inside, and disappear outside, the event.</summary>
    [Fact]
    public void IntermittentEnergyIsConfinedToEventInterval()
    {
        var data = Load("12-IntermittentFault160");
        var frames = Spectrogram(data).Frames;
        var ev = data.Expected.GetProperty("event");
        var start = ev.GetProperty("startSeconds").GetDouble();
        var end = ev.GetProperty("endSeconds").GetDouble();
        // Exclude windows crossing event boundaries; their mixtures are intentional.
        var inside = frames.Where(f => f.CenterTimeSeconds > start + 0.256 && f.CenterTimeSeconds < end - 0.256).ToArray();
        var outside = frames.Where(f => f.CenterTimeSeconds < start - 0.256 || f.CenterTimeSeconds > end + 0.256).ToArray();
        Assert.NotEmpty(inside);
        Assert.NotEmpty(outside);
        foreach (var hz in ev.GetProperty("harmonicsHz").EnumerateArray().Select(v => v.GetDouble())
            .Prepend(ev.GetProperty("fundamentalHz").GetDouble()))
        {
            double At(SpectrogramFrame f) => f.Bins.MinBy(b => Math.Abs(b.FrequencyHz - hz))!.Amplitude;
            Assert.True(inside.Min(At) > 5 * outside.Max(At), $"Event contrast at {hz} Hz");
        }
    }

    /// <summary>Compares motor-related spectral evidence without assigning a physical fault diagnosis.</summary>
    [Fact]
    public void FourMotorComparisonFindsElevatedMotorThreeHarmonics()
    {
        var healthy = Load("14-FourMotorHealthy");
        var elevated = Load("15-FourMotorMotor3Fault");
        var baseline = Spectrum(healthy);
        var current = Spectrum(elevated);
        foreach (var hz in healthy.Expected.GetProperty("motorFundamentalsHz").EnumerateArray())
        {
            Assert.Contains(Peaks(baseline), p => Math.Abs(p.FrequencyHz - hz.GetDouble()) <= baseline.ResolutionHz);
        }
        var injected = elevated.Expected.GetProperty("injectedFault");
        var differences = new SpectrumComparer().Compare(baseline, current);
        var fundamental = injected.GetProperty("fundamentalHz").GetDouble();
        Assert.True(Amplitude(current, fundamental) > Amplitude(baseline, fundamental));
        foreach (var hz in injected.GetProperty("strongHarmonicsHz").EnumerateArray().Select(v => v.GetDouble()))
        {
            var row = differences.MinBy(d => Math.Abs(d.FrequencyHz - hz))!;
            Assert.True(row.Current > 2 * row.Baseline, $"Elevated evidence at {hz} Hz");
        }
    }

    /// <summary>Checks the supplied ordering independently at both injected harmonic frequencies.</summary>
    [Fact]
    public void AxisCouplingFollowsDeclaredAmplitudeOrdering()
    {
        var data = Load("16-ThreeAxisMechanical");
        var spectra = data.Expected.GetProperty("expectedAxisOrdering").EnumerateArray()
            .Select(axis => Spectrum(data, axis.GetString()!)).ToArray();
        foreach (var hz in data.Expected.GetProperty("dominantFamilyHz").EnumerateArray())
        {
            var amplitudes = spectra.Select(s => Amplitude(s, hz.GetDouble())).ToArray();
            Assert.True(amplitudes[0] > amplitudes[1] && amplitudes[1] > amplitudes[2]);
            Assert.All(spectra, s => Assert.Contains(Peaks(s), p => Math.Abs(p.FrequencyHz - hz.GetDouble()) <= s.ResolutionHz));
        }
    }

    private static FrequencySpectrum Spectrum(Dataset data, string channel = "gyro_x") =>
        new SpectrumAverager(new FftAnalyzer()).Analyze(data.Columns[channel], data.Rate,
            new FftOptions { Size = 4096 }, TestContext.Current.CancellationToken);

    private static Spectrogram Spectrogram(Dataset data) =>
        new SpectrogramAnalyzer(new FftAnalyzer()).Analyze(data.Columns["gyro_x"], data.Rate,
            new SpectrogramOptions { Fft = new FftOptions { Size = 512 }, OverlapSamples = 256 },
            TestContext.Current.CancellationToken);

    private static ImmutableArray<SpectralPeak> Peaks(FrequencySpectrum spectrum) =>
        new PeakDetector().Detect(spectrum, new PeakDetectionOptions());

    private static double Amplitude(FrequencySpectrum spectrum, double hz) =>
        spectrum.Bins.MinBy(b => Math.Abs(b.FrequencyHz - hz))!.Amplitude;

    private static IEnumerable<double> ExpectedTones(JsonElement metadata)
    {
        foreach (var key in new[] { "expectedPeaksHz", "expectedPeaksAfterDcRemovalHz", "expectedHarmonicsHz" })
        {
            if (metadata.TryGetProperty(key, out var values))
            {
                foreach (var value in values.EnumerateArray())
                {
                    yield return value.GetDouble();
                }
            }
        }
        foreach (var key in new[] { "expectedPeakHz", "expectedFundamentalHz" })
        {
            if (metadata.TryGetProperty(key, out var value))
            {
                yield return value.GetDouble();
            }
        }
    }

    private static Dataset Load(string name)
    {
        var path = Path.Combine(FixtureRoot, name);
        using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(path, "expected.json")));
        using var reader = File.OpenText(Path.Combine(path, "samples.csv"));
        var headers = reader.ReadLine()!.Split(',');
        var columns = headers.ToDictionary(h => h, _ => new List<double>());
        while (reader.ReadLine() is { } line)
        {
            var cells = line.Split(',');
            Assert.Equal(headers.Length, cells.Length);
            for (var i = 0; i < cells.Length; i++)
            {
                var value = double.Parse(cells[i], CultureInfo.InvariantCulture);
                Assert.True(double.IsFinite(value));
                columns[headers[i]].Add(value);
            }
        }
        return new Dataset(metadata.RootElement.Clone(), columns.ToDictionary(p => p.Key, p => p.Value.ToArray()));
    }

    private sealed record Dataset(JsonElement Expected, Dictionary<string, double[]> Columns)
    {
        internal double Rate => Number("sampleRateHz");
        internal double Number(string name) => Expected.GetProperty(name).GetDouble();
    }
}
