using System.Collections.Immutable;

namespace MissionPlanner.Analysis.Frequency;

/// <summary>One-sided peak amplitude in the input signal's units, not power or PSD.</summary>
/// <param name="FrequencyHz">Bin center frequency in hertz.</param>
/// <param name="Amplitude">Coherent-gain-corrected peak amplitude.</param>
public sealed record FrequencyBin(double FrequencyHz, double Amplitude);

/// <summary>Numerical spectrum and provenance for one complete input window.</summary>
public sealed class FrequencySpectrum
{
    internal FrequencySpectrum(double sampleRateHz, FftOptions options, double removedMean, ImmutableArray<FrequencyBin> bins)
    {
        SampleRateHz = sampleRateHz;
        Options = options;
        RemovedMean = removedMean;
        Bins = bins;
    }

    /// <summary>Uniform sample rate supplied by the caller.</summary>
    public double SampleRateHz { get; }
    /// <summary>Transform size, taper and mean-removal settings.</summary>
    public FftOptions Options { get; }
    /// <summary>Mean subtracted before windowing, or zero if disabled.</summary>
    public double RemovedMean { get; }
    /// <summary>Bin spacing; this is not a claim that nearby tones can be resolved.</summary>
    public double ResolutionHz => SampleRateHz / Options.Size;
    /// <summary>Highest representable frequency. Aliasing in source data cannot be detected here.</summary>
    public double NyquistHz => SampleRateHz / 2;
    /// <summary>Immutable bins including DC and Nyquist.</summary>
    public ImmutableArray<FrequencyBin> Bins { get; }
}
