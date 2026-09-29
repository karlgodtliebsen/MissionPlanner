using System.Collections.Immutable;
using MissionPlanner.Analysis.Frequency;

namespace MissionPlanner.Analysis.Vibration;

/// <summary>Coordinate axis attached to the source; no frame convention is inferred.</summary>
public enum VibrationAxis
{
    /// <summary>First source coordinate.</summary>
    X,
    /// <summary>Second source coordinate.</summary>
    Y,
    /// <summary>Third source coordinate.</summary>
    Z
}

/// <summary>Signal identity supplied by an adapter without protocol-specific types.</summary>
/// <param name="Name">Human-readable sensor or source identifier.</param>
/// <param name="Axis">Source coordinate.</param>
/// <param name="Unit">Amplitude unit, for example rad/s or m/s².</param>
public sealed record VibrationSource(string Name, VibrationAxis Axis, string Unit);

/// <summary>Conservative interpretation linked directly to its measured evidence.</summary>
/// <param name="Description">Qualified explanation; never a specific defect diagnosis.</param>
/// <param name="Series">Harmonic evidence supporting the interpretation.</param>
/// <param name="Limitation">Information still needed to identify a cause.</param>
public sealed record VibrationAssessment(string Description, HarmonicSeries Series, string Limitation);

/// <summary>Structured observations kept separate from interpretations.</summary>
/// <param name="Source">Source identity and units.</param>
/// <param name="Spectrum">All numerical FFT evidence and transform settings.</param>
/// <param name="Peaks">Measured local maxima and detection thresholds.</param>
/// <param name="Harmonics">Candidate harmonic relationships.</param>
/// <param name="Assessments">Qualified interpretations referring to harmonic evidence.</param>
public sealed record VibrationAnalysis(VibrationSource Source, FrequencySpectrum Spectrum,
    ImmutableArray<SpectralPeak> Peaks, ImmutableArray<HarmonicSeries> Harmonics,
    ImmutableArray<VibrationAssessment> Assessments);

/// <summary>Analyzes one source axis without inferring RPM, resonance or a mechanical defect.</summary>
public sealed class VibrationAnalyzer
{
    private readonly FftAnalyzer fft;
    private readonly PeakDetector peaks;
    private readonly HarmonicDetector harmonics;

    /// <summary>Composes reusable numerical analyzers.</summary>
    /// <param name="fft">Injected FFT implementation.</param>
    /// <param name="peaks">Injected peak detector.</param>
    /// <param name="harmonics">Injected harmonic detector.</param>
    public VibrationAnalyzer(FftAnalyzer fft, PeakDetector peaks, HarmonicDetector harmonics)
    {
        ArgumentNullException.ThrowIfNull(fft);
        ArgumentNullException.ThrowIfNull(peaks);
        ArgumentNullException.ThrowIfNull(harmonics);
        this.fft = fft;
        this.peaks = peaks;
        this.harmonics = harmonics;
    }

    /// <summary>Retains the full spectrum and peak evidence behind each assessment.</summary>
    /// <param name="samples">One complete uniform sample window.</param>
    /// <param name="sampleRateHz">Source sample rate.</param>
    /// <param name="source">Signal identity and units.</param>
    /// <param name="fftOptions">Transform configuration.</param>
    /// <param name="peakOptions">Detection thresholds.</param>
    /// <returns>Observations and qualified harmonic assessments; silence is not a health verdict.</returns>
    public VibrationAnalysis Analyze(ReadOnlySpan<double> samples, double sampleRateHz, VibrationSource source,
        FftOptions fftOptions, PeakDetectionOptions peakOptions)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrWhiteSpace(source.Name) || string.IsNullOrWhiteSpace(source.Unit) || !Enum.IsDefined(source.Axis))
        {
            throw new ArgumentException("Source name, unit and a valid axis are required.", nameof(source));
        }
        var spectrum = fft.Analyze(samples, sampleRateHz, fftOptions);
        var detectedPeaks = peaks.Detect(spectrum, peakOptions);
        var series = harmonics.Detect(detectedPeaks, spectrum.ResolutionHz);
        var assessments = series.Select(s => new VibrationAssessment(
            "Measured peaks form a candidate harmonic series consistent with periodic excitation.", s,
            "The source is undetermined. Motor/propeller attribution requires time-aligned RPM evidence; spectral peaks alone do not identify a mechanical defect."
        )).ToImmutableArray();
        return new VibrationAnalysis(source, spectrum, detectedPeaks, series, assessments);
    }
}
