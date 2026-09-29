using System.Collections.Immutable;
using System.Numerics;

namespace MissionPlanner.Analysis.Frequency;

/// <summary>Stateless radix-two FFT for finite, uniformly sampled real signals.</summary>
public sealed class FftAnalyzer
{
    /// <summary>Transforms exactly one window into a one-sided amplitude spectrum.</summary>
    /// <param name="samples">Finite samples; irregular sampling must be handled upstream.</param>
    /// <param name="sampleRateHz">Finite positive sample frequency.</param>
    /// <param name="options">Transform length, window and DC settings.</param>
    /// <returns>Immutable normalized bins including DC and Nyquist.</returns>
    public FrequencySpectrum Analyze(ReadOnlySpan<double> samples, double sampleRateHz, FftOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        if (!double.IsFinite(sampleRateHz) || sampleRateHz <= 0 || sampleRateHz / options.Size == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRateHz));
        }
        if (samples.Length != options.Size)
        {
            throw new ArgumentException("Supply exactly Size samples; padding and truncation are not implicit.", nameof(samples));
        }

        var mean = 0.0;
        foreach (var sample in samples)
        {
            if (!double.IsFinite(sample))
            {
                throw new ArgumentException("Samples must be finite.", nameof(samples));
            }
            mean += sample / samples.Length;
        }
        mean = options.RemoveDc ? mean : 0;
        var values = new Complex[samples.Length];
        var gain = 0.0;
        for (var i = 0; i < samples.Length; i++)
        {
            var weight = options.Window == WindowFunction.Hann
                ? 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / samples.Length)
                : 1.0;
            gain += weight;
            values[i] = (samples[i] - mean) * weight;
        }

        for (int i = 1, j = 0; i < values.Length; i++)
        {
            var bit = values.Length >> 1;
            for (; (j & bit) != 0; bit >>= 1)
            {
                j ^= bit;
            }
            j ^= bit;
            if (i < j)
            {
                (values[i], values[j]) = (values[j], values[i]);
            }
        }
        for (var length = 2; ; length *= 2)
        {
            var step = Complex.FromPolarCoordinates(1, -2 * Math.PI / length);
            for (var start = 0; start < values.Length; start += length)
            {
                var phase = Complex.One;
                for (var offset = 0; offset < length / 2; offset++)
                {
                    var even = values[start + offset];
                    var odd = values[start + offset + length / 2] * phase;
                    values[start + offset] = even + odd;
                    values[start + offset + length / 2] = even - odd;
                    phase *= step;
                }
            }
            if (length == values.Length)
            {
                break;
            }
        }

        var bins = ImmutableArray.CreateBuilder<FrequencyBin>(samples.Length / 2 + 1);
        for (var i = 0; i <= samples.Length / 2; i++)
        {
            var factor = i == 0 || i == samples.Length / 2 ? 1 : 2;
            var amplitude = values[i].Magnitude / gain * factor;
            if (!double.IsFinite(amplitude))
            {
                throw new ArgumentException("Sample dynamic range overflowed the transform.", nameof(samples));
            }
            bins.Add(new FrequencyBin(i * (sampleRateHz / samples.Length), amplitude));
        }
        return new FrequencySpectrum(sampleRateHz, options, mean, bins.MoveToImmutable());
    }
}
