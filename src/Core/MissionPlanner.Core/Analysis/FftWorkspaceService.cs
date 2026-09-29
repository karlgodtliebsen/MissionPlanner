using System.Collections.Immutable;
using MissionPlanner.Analysis.Frequency;
using MissionPlanner.Analysis.Vibration;

namespace MissionPlanner.Core.Analysis;

/// <summary>Analysis choices captured before starting background computation.</summary>
/// <param name="StartSeconds">Inclusive source-log start time.</param>
/// <param name="EndSeconds">Inclusive source-log end time.</param>
/// <param name="FftSize">Power-of-two window length.</param>
/// <param name="MinimumHz">Lower retained spectrogram frequency.</param>
/// <param name="MaximumHz">Upper retained frequency, null for Nyquist.</param>
public sealed record FftAnalysisRequest(double StartSeconds, double EndSeconds, int FftSize, double MinimumHz, double? MaximumHz);

/// <summary>A retained measured baseline with explicit units and provenance.</summary>
/// <param name="Name">Dataset label.</param>
/// <param name="Unit">Physical amplitude unit.</param>
/// <param name="Spectrum">Measured spectrum.</param>
public sealed record FftBaseline(string Name, string Unit, FrequencySpectrum Spectrum);

/// <summary>Peak location relative to nominal static filter regions.</summary>
/// <param name="Peak">Measured peak evidence.</param>
/// <param name="InsideStaticBand">Whether a supported nominal band contains the frequency.</param>
/// <param name="Assessment">Qualified static coverage description.</param>
public sealed record NotchPeakCoverage(SpectralPeak Peak, bool InsideStaticBand, string Assessment);

/// <summary>Immutable analysis output consumed by presentation and exports.</summary>
/// <param name="Source">Uniform source segment.</param>
/// <param name="StartSeconds">Actual selected first sample time.</param>
/// <param name="Spectrum">Measured mean-amplitude spectrum.</param>
/// <param name="Spectrogram">Measured time-frequency frames relative to StartSeconds.</param>
/// <param name="Peaks">Peak observations.</param>
/// <param name="Harmonics">Harmonic candidates.</param>
/// <param name="Correlations">Actual RPM evidence paired to independently selected dominant peaks.</param>
/// <param name="Resonances">Localized amplification candidates.</param>
/// <param name="Comparison">Optional baseline differences on a common frequency grid.</param>
/// <param name="Coverage">Static filter-band assessment.</param>
/// <param name="CurrentSimulation">Generic current-static-configuration prediction.</param>
/// <param name="ProposedSimulation">Generic proposed-configuration prediction.</param>
/// <param name="Notes">Interpretation and source limitations.</param>
public sealed record FftWorkspaceResult(ImuSampleSeries Source, double StartSeconds, FrequencySpectrum Spectrum, Spectrogram Spectrogram,
    ImmutableArray<SpectralPeak> Peaks, ImmutableArray<HarmonicSeries> Harmonics,
    ImmutableArray<MotorFrequencyCorrelation> Correlations, ImmutableArray<ResonanceCandidate> Resonances,
    ImmutableArray<SpectrumDifference> Comparison, ImmutableArray<NotchPeakCoverage> Coverage,
    ImmutableArray<SimulatedFrequencyBin> CurrentSimulation, ImmutableArray<SimulatedFrequencyBin> ProposedSimulation,
    ImmutableArray<string> Notes)
{
    /// <summary>Exact captured interval and transform choices.</summary>
    public FftAnalysisRequest? Request { get; init; }
    /// <summary>Read-only parameter snapshot used by this completed result.</summary>
    public NotchParameterSnapshot? ParameterSnapshot { get; init; }
    /// <summary>Exact generic proposed filter configurations used by this result.</summary>
    public ImmutableArray<NotchFilter> ProposedFilters { get; init; } = [];
    /// <summary>Measured baseline and provenance used for comparison.</summary>
    public FftBaseline? Baseline { get; init; }
}

/// <summary>Coordinates offline DSP; views only select inputs and display immutable outputs.</summary>
public sealed class FftWorkspaceService(SpectrumAverager average, SpectrogramAnalyzer spectrograms, PeakDetector peaks,
    HarmonicDetector harmonics, MotorFrequencyCorrelator correlator, ResonanceDetector resonances,
    SpectrumComparer comparer, NotchFilterSimulator simulator)
{
    /// <summary>Analyzes a selected interval without accessing UI, transports or vehicle writes.</summary>
    /// <param name="log">Decoded source artifact.</param>
    /// <param name="source">Selected uniform segment from the artifact.</param>
    /// <param name="request">Captured interval and FFT choices.</param>
    /// <param name="baseline">Optional reference in matching physical units.</param>
    /// <param name="parameters">Optional captured parameter configuration.</param>
    /// <param name="proposed">Optional offline static notch proposal.</param>
    /// <param name="token">Cancellation between transform windows.</param>
    /// <returns>Structured measured evidence and clearly separate predictions.</returns>
    public FftWorkspaceResult Analyze(ImuLogData log, ImuSampleSeries source, FftAnalysisRequest request,
        FftBaseline? baseline, NotchParameterSnapshot? parameters, IReadOnlyList<NotchFilter> proposed, CancellationToken token)
    {
        if (!log.Series.Contains(source) || !double.IsFinite(request.StartSeconds) || !double.IsFinite(request.EndSeconds) ||
            request.EndSeconds < request.StartSeconds || request.StartSeconds < source.StartTimeSeconds - 1e-8 ||
            request.EndSeconds > source.EndTimeSeconds + 1e-8 || request.FftSize is < 16 or > 16384)
        {
            throw new ArgumentException("Choose an interval inside the selected segment and FFT size 16–16384.");
        }
        var first = Math.Max(0, (int)Math.Ceiling((request.StartSeconds - source.StartTimeSeconds) * source.SampleRateHz - 1e-7));
        var last = Math.Min(source.SampleCount - 1, (int)Math.Floor((request.EndSeconds - source.StartTimeSeconds) * source.SampleRateHz + 1e-7));
        var count = last - first + 1;
        if (count < request.FftSize)
        {
            throw new ArgumentException("The selected interval must contain at least one full FFT window.");
        }
        var frameCount = 1 + (count - request.FftSize) / (request.FftSize / 2);
        if (frameCount > 2048 || (long)frameCount * (request.FftSize / 2 + 1) > 2_000_000)
        {
            throw new ArgumentException("Select a shorter interval (maximum 2048 frames and two million time-frequency bins).");
        }
        var options = new FftOptions { Size = request.FftSize };
        var samples = source.Samples.AsSpan(first, count);
        var spectrum = average.Analyze(samples, source.SampleRateHz, options, token);
        var frames = spectrograms.Analyze(samples, source.SampleRateHz, new SpectrogramOptions
        {
            Fft = options, OverlapSamples = request.FftSize / 2,
            MinimumFrequencyHz = request.MinimumHz, MaximumFrequencyHz = request.MaximumHz
        }, token);
        var detected = peaks.Detect(spectrum, new PeakDetectionOptions());
        var relationships = harmonics.Detect(detected, spectrum.ResolutionHz);
        var correlationResults = ImmutableArray.CreateBuilder<MotorFrequencyCorrelation>();
        var candidates = ImmutableArray.CreateBuilder<ResonanceCandidate>();
        var notes = ImmutableArray.CreateBuilder<string>();
        notes.AddRange(log.Diagnostics);
        var start = source.StartTimeSeconds + first / source.SampleRateHz;
        foreach (var group in log.MotorSamples.Where(m => m.IsRpm).GroupBy(m => (m.Index, m.Source)))
        {
            var speeds = group.OrderBy(m => m.TimeSeconds).ToArray();
            var pairs = ImmutableArray.CreateBuilder<MotorFrequencyObservation>();
            var position = 0;
            foreach (var frame in frames.Frames)
            {
                var time = start + frame.CenterTimeSeconds;
                while (position + 1 < speeds.Length && speeds[position + 1].TimeSeconds <= time)
                {
                    position++;
                }
                if (position + 1 >= speeds.Length || speeds[position].TimeSeconds > time ||
                    speeds[position + 1].TimeSeconds - speeds[position].TimeSeconds is <= 0 or > 0.5)
                {
                    continue;
                }
                var left = speeds[position];
                var right = speeds[position + 1];
                var rpm = left.Value + (right.Value - left.Value) * (time - left.TimeSeconds) / (right.TimeSeconds - left.TimeSeconds);
                var peak = frame.Bins.Where(b => b.FrequencyHz > 0).MaxBy(b => b.Amplitude);
                if (rpm > 0 && peak is { Amplitude: > 0.01 })
                {
                    pairs.Add(new MotorFrequencyObservation(time, rpm, peak.FrequencyHz, peak.Amplitude));
                }
            }
            if (pairs.Count >= 3)
            {
                var correlation = correlator.Analyze(group.Key.Index, group.Key.Source, pairs.ToImmutable());
                correlationResults.Add(correlation);
                var candidate = resonances.Detect(correlation, Math.Max(10, spectrum.ResolutionHz * 4));
                if (candidate is not null)
                {
                    candidates.Add(candidate);
                }
            }
        }
        if (log.MotorSamples.Any(m => !m.IsRpm))
        {
            notes.Add("Output PWM is present but is not converted to RPM. Shaft-frequency correlation requires actual RPM telemetry.");
        }
        notes.Add("RPM is interpolated only between measurements no more than 0.5 s apart. Correlation follows the dominant measured band at shaft order 1; it does not identify a motor defect.");
        notes.Add($"Spectrum: mean linear amplitude of complete 50%-overlapped Hann windows. Unused tail: {frames.UnusedTailSamples} samples. Harmonics and resonance results are candidates, not diagnoses.");
        var differences = ImmutableArray<SpectrumDifference>.Empty;
        if (baseline is not null)
        {
            if (baseline.Unit != source.Unit)
            {
                throw new ArgumentException("Baseline and current signal units must match.");
            }
            differences = comparer.Compare(baseline.Spectrum, spectrum);
            notes.Add($"Baseline: {baseline.Name}. Amplitudes interpolated onto the coarser frequency grid; leakage/scalloping can differ with resolution.");
        }
        var currentFilters = parameters?.StaticFilters ?? [];
        if (currentFilters.Any(f => f.CenterHz >= spectrum.NyquistHz))
        {
            notes.Add("Configured harmonics at or above Nyquist are excluded from this simulation.");
        }
        var supported = currentFilters.Where(f => f.CenterHz < spectrum.NyquistHz).ToArray();
        var coverage = parameters is null ? ImmutableArray<NotchPeakCoverage>.Empty : detected.Select(p =>
        {
            var inside = supported.Any(f => Math.Abs(p.FrequencyHz - f.CenterHz) <= f.BandwidthHz / 2);
            return new NotchPeakCoverage(p, inside, inside ? "Inside a supported nominal static band; attenuation is not measured."
                : "Outside supported static bands; dynamic or unavailable settings may still apply.");
        }).ToImmutableArray();
        if (parameters is not null)
        {
            notes.AddRange(parameters.Limitations);
        }
        return new FftWorkspaceResult(source, start, spectrum, frames, detected, relationships,
            correlationResults.ToImmutable(), candidates.ToImmutable(), differences, coverage,
            supported.Length == 0 ? [] : simulator.Simulate(spectrum, supported),
            proposed.Count == 0 ? [] : simulator.Simulate(spectrum, proposed), notes.ToImmutable())
        {
            Request = request, ParameterSnapshot = parameters, ProposedFilters = proposed.ToImmutableArray(), Baseline = baseline
        };
    }
}
