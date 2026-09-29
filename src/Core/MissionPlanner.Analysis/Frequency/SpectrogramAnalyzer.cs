using System.Collections.Immutable;

namespace MissionPlanner.Analysis.Frequency;

/// <summary>STFT settings; only complete windows are transformed.</summary>
public sealed record SpectrogramOptions
{
    /// <summary>Window length, taper and mean removal.</summary>
    public FftOptions Fft { get; init; } = new();
    /// <summary>Number of samples shared by adjacent windows; must be less than the window length.</summary>
    public int OverlapSamples { get; init; } = 512;
    /// <summary>Lowest retained bin frequency, inclusive.</summary>
    public double MinimumFrequencyHz { get; init; }
    /// <summary>Highest retained bin frequency, inclusive; null means Nyquist.</summary>
    public double? MaximumFrequencyHz { get; init; }
}

/// <summary>A complete STFT window with timing and amplitude evidence.</summary>
/// <param name="StartSample">Offset of the first sample in the original sequence.</param>
/// <param name="CenterTimeSeconds">Center of the sampled interval relative to input start.</param>
/// <param name="Bins">Immutable bins within the requested frequency range.</param>
public sealed record SpectrogramFrame(int StartSample, double CenterTimeSeconds, ImmutableArray<FrequencyBin> Bins);

/// <summary>Time-frequency evidence and the settings needed to interpret it.</summary>
/// <param name="SampleRateHz">Source sample rate.</param>
/// <param name="InputSampleCount">Original sequence length.</param>
/// <param name="UnusedTailSamples">Samples after the final complete window.</param>
/// <param name="Options">Transform, overlap and frequency range settings.</param>
/// <param name="Frames">Frames in ascending time order.</param>
public sealed record Spectrogram(double SampleRateHz, int InputSampleCount, int UnusedTailSamples,
    SpectrogramOptions Options, ImmutableArray<SpectrogramFrame> Frames);

/// <summary>Computes overlapping FFT windows without presentation or scheduling dependencies.</summary>
public sealed class SpectrogramAnalyzer
{
    private readonly FftAnalyzer fft;

    /// <summary>Creates an STFT analyzer using the shared FFT implementation.</summary>
    /// <param name="fft">Injected stateless FFT analyzer.</param>
    public SpectrogramAnalyzer(FftAnalyzer fft)
    {
        ArgumentNullException.ThrowIfNull(fft);
        this.fft = fft;
    }

    /// <summary>Analyzes all complete windows. Callers choose an appropriate worker thread.</summary>
    /// <param name="samples">Uniformly spaced finite samples, at least one full window.</param>
    /// <param name="sampleRateHz">Finite positive sample rate.</param>
    /// <param name="options">STFT configuration.</param>
    /// <param name="cancellationToken">Cancellation observed between windows.</param>
    /// <returns>Frames plus the explicitly reported unused tail length.</returns>
    public Spectrogram Analyze(ReadOnlySpan<double> samples, double sampleRateHz, SpectrogramOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Fft);
        options.Fft.Validate();
        if (!double.IsFinite(sampleRateHz) || sampleRateHz <= 0 || sampleRateHz / options.Fft.Size == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRateHz));
        }
        var maximum = options.MaximumFrequencyHz ?? sampleRateHz / 2;
        if (options.OverlapSamples < 0 || options.OverlapSamples >= options.Fft.Size ||
            !double.IsFinite(options.MinimumFrequencyHz) || options.MinimumFrequencyHz < 0 ||
            !double.IsFinite(maximum) || maximum < options.MinimumFrequencyHz || maximum > sampleRateHz / 2)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
        if (samples.Length < options.Fft.Size)
        {
            throw new ArgumentException("At least one full window is required.", nameof(samples));
        }
        foreach (var value in samples)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentException("All samples, including the unused tail, must be finite.", nameof(samples));
            }
        }
        var frames = ImmutableArray.CreateBuilder<SpectrogramFrame>();
        var hop = options.Fft.Size - options.OverlapSamples;
        var lastStart = 0;
        for (long offset = 0; offset <= samples.Length - options.Fft.Size; offset += hop)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var start = (int)offset;
            var spectrum = fft.Analyze(samples.Slice(start, options.Fft.Size), sampleRateHz, options.Fft);
            var bins = spectrum.Bins.Where(b => b.FrequencyHz >= options.MinimumFrequencyHz && b.FrequencyHz <= maximum).ToImmutableArray();
            if (bins.IsEmpty)
            {
                throw new ArgumentException("The requested frequency interval contains no FFT bin centers.", nameof(options));
            }
            frames.Add(new SpectrogramFrame(start, (start + (options.Fft.Size - 1) / 2.0) / sampleRateHz, bins));
            lastStart = start;
        }
        return new Spectrogram(sampleRateHz, samples.Length, samples.Length - lastStart - options.Fft.Size, options, frames.ToImmutable());
    }
}
