using System.Numerics;

namespace MissionPlanner.Core.Setup.OptionalHardware;

public sealed record FftPeak(double FrequencyHz, double Magnitude);
public sealed record FftSpectrum(double SampleRateHz, IReadOnlyList<double> Frequencies, IReadOnlyList<double> Magnitudes, FftPeak Peak);
public interface IFftAnalysisService { FftSpectrum Analyze(IReadOnlyList<double> samples, double sampleRateHz); }

public sealed class FftAnalysisService : IFftAnalysisService
{
    public FftSpectrum Analyze(IReadOnlyList<double> samples, double sampleRateHz)
    {
        if (samples.Count < 2) throw new ArgumentException("At least two samples are required.", nameof(samples));
        if (sampleRateHz <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRateHz));
        var bins = samples.Count / 2 + 1;
        var frequencies = new double[bins];
        var magnitudes = new double[bins];
        for (var k = 0; k < bins; k++)
        {
            Complex sum = Complex.Zero;
            for (var n = 0; n < samples.Count; n++)
            {
                var window = .5 - .5 * Math.Cos(2 * Math.PI * n / (samples.Count - 1));
                sum += samples[n] * window * Complex.FromPolarCoordinates(1, -2 * Math.PI * k * n / samples.Count);
            }
            frequencies[k] = k * sampleRateHz / samples.Count;
            magnitudes[k] = 2 * sum.Magnitude / samples.Count;
        }
        var peakIndex = Enumerable.Range(1, Math.Max(1, bins - 1)).MaxBy(i => magnitudes[i]);
        return new FftSpectrum(sampleRateHz, frequencies, magnitudes, new FftPeak(frequencies[peakIndex], magnitudes[peakIndex]));
    }
}
