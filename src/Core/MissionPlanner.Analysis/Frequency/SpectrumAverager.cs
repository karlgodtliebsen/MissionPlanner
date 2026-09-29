using System.Collections.Immutable;

namespace MissionPlanner.Analysis.Frequency;

/// <summary>Produces arithmetic mean amplitudes over complete, overlapping windows.</summary>
public sealed class SpectrumAverager(FftAnalyzer fft)
{
    /// <summary>Averages linear amplitudes, not decibels, retaining the FFT normalization.</summary>
    /// <param name="samples">Uniform source sequence.</param>
    /// <param name="sampleRateHz">Sample rate.</param>
    /// <param name="options">FFT configuration.</param>
    /// <param name="cancellationToken">Cancellation between windows.</param>
    /// <returns>Mean-amplitude spectrum over 50%-overlapped complete windows.</returns>
    public FrequencySpectrum Analyze(ReadOnlySpan<double> samples, double sampleRateHz, FftOptions options, CancellationToken cancellationToken = default)
    {
        options.Validate();
        if (samples.Length < options.Size)
        {
            throw new ArgumentException("At least one FFT window is required.", nameof(samples));
        }
        var sums = new double[options.Size / 2 + 1];
        var count = 0;
        var mean = 0.0;
        for (var offset = 0; offset <= samples.Length - options.Size; offset += options.Size / 2)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var spectrum = fft.Analyze(samples.Slice(offset, options.Size), sampleRateHz, options);
            count++;
            mean += (spectrum.RemovedMean - mean) / count;
            for (var i = 0; i < sums.Length; i++)
            {
                sums[i] += (spectrum.Bins[i].Amplitude - sums[i]) / count;
            }
        }
        return new FrequencySpectrum(sampleRateHz, options, mean,
            sums.Select((value, index) => new FrequencyBin(index * sampleRateHz / options.Size, value)).ToImmutableArray());
    }
}
