using MissionPlanner.Analysis.Frequency;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Strongest non-DC frequency in the compatibility sample-text workflow.</summary>
/// <param name="FrequencyHz">Measured bin frequency.</param>
/// <param name="Magnitude">Normalized peak amplitude in source units.</param>
public sealed record FftPeak(double FrequencyHz, double Magnitude);

/// <summary>Compatibility projection of the reusable FFT domain.</summary>
/// <param name="SampleRateHz">Input rate.</param>
/// <param name="Frequencies">One-sided bin centers.</param>
/// <param name="Magnitudes">Normalized peak amplitudes.</param>
/// <param name="Peak">Largest non-DC bin.</param>
public sealed record FftSpectrum(double SampleRateHz, IReadOnlyList<double> Frequencies, IReadOnlyList<double> Magnitudes, FftPeak Peak)
{
    /// <summary>Length of the largest complete power-of-two window used from the input start.</summary>
    public int SamplesUsed { get; init; }
    /// <summary>Explicitly unprocessed samples after the selected window.</summary>
    public int UnusedTailSamples { get; init; }
}

/// <summary>Compatibility boundary for manual numerical sample input.</summary>
public interface IFftAnalysisService
{
    /// <summary>Analyzes the largest complete power-of-two prefix and reports its unused tail.</summary>
    /// <param name="samples">At least four finite values.</param>
    /// <param name="sampleRateHz">Finite positive sampling rate.</param>
    /// <returns>Normalized spectrum with explicit sample accounting.</returns>
    FftSpectrum Analyze(IReadOnlyList<double> samples, double sampleRateHz);
}

/// <summary>Adapts manual samples to the shared FFT; no separate DFT remains.</summary>
public sealed class FftAnalysisService(FftAnalyzer fft) : IFftAnalysisService
{
    /// <inheritdoc />
    public FftSpectrum Analyze(IReadOnlyList<double> samples, double sampleRateHz)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count < 4 || samples.Any(v => !double.IsFinite(v)))
        {
            throw new ArgumentException("Supply at least four finite samples.", nameof(samples));
        }
        var size = 4;
        while (size <= samples.Count / 2)
        {
            size *= 2;
        }
        var spectrum = fft.Analyze(samples.Take(size).ToArray(), sampleRateHz, new FftOptions { Size = size });
        var peak = spectrum.Bins.Skip(1).MaxBy(b => b.Amplitude)!;
        return new FftSpectrum(sampleRateHz, spectrum.Bins.Select(b => b.FrequencyHz).ToArray(),
            spectrum.Bins.Select(b => b.Amplitude).ToArray(), new FftPeak(peak.FrequencyHz, peak.Amplitude))
        {
            SamplesUsed = size, UnusedTailSamples = samples.Count - size
        };
    }
}
