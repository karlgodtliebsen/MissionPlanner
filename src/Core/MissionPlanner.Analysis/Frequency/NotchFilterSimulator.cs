using System.Collections.Immutable;
using System.Numerics;

namespace MissionPlanner.Analysis.Frequency;

/// <summary>Generic static notch model; not a firmware-exact filter implementation.</summary>
/// <param name="CenterHz">Center frequency.</param>
/// <param name="BandwidthHz">Nominal bandwidth (Q = center/bandwidth).</param>
/// <param name="AttenuationDb">Attenuation at center in positive decibels.</param>
public sealed record NotchFilter(double CenterHz, double BandwidthHz, double AttenuationDb);

/// <summary>Explicitly simulated amplitudes alongside their measured inputs.</summary>
/// <param name="FrequencyHz">Bin frequency.</param>
/// <param name="MeasuredAmplitude">Original measured amplitude.</param>
/// <param name="SimulatedAmplitude">Predicted amplitude from the generic static model.</param>
/// <param name="Gain">Combined modeled transfer magnitude.</param>
public sealed record SimulatedFrequencyBin(double FrequencyHz, double MeasuredAmplitude, double SimulatedAmplitude, double Gain);

/// <summary>Applies a generic digital biquad-notch transfer response to measured bins offline.</summary>
public sealed class NotchFilterSimulator
{
    /// <summary>Predicts a static cascaded response; phase, transients and firmware tracking are not simulated.</summary>
    /// <param name="spectrum">Measured source spectrum.</param>
    /// <param name="filters">Proposed notches entirely below Nyquist.</param>
    /// <returns>Measured and simulated values, kept distinct.</returns>
    public ImmutableArray<SimulatedFrequencyBin> Simulate(FrequencySpectrum spectrum, IReadOnlyList<NotchFilter> filters)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        ArgumentNullException.ThrowIfNull(filters);
        foreach (var filter in filters)
        {
            if (!double.IsFinite(filter.CenterHz) || !double.IsFinite(filter.BandwidthHz) || !double.IsFinite(filter.AttenuationDb) ||
                filter.CenterHz <= 0 || filter.CenterHz >= spectrum.NyquistHz || filter.BandwidthHz <= 0 ||
                filter.BandwidthHz > filter.CenterHz || filter.AttenuationDb < 0 || filter.AttenuationDb > 120)
            {
                throw new ArgumentException("Notches require 0 < bandwidth <= center < Nyquist and attenuation 0–120 dB.", nameof(filters));
            }
        }
        return spectrum.Bins.Select(bin =>
        {
            var gain = 1.0;
            foreach (var filter in filters)
            {
                var omega = 2 * Math.PI * filter.CenterHz / spectrum.SampleRateHz;
                var alpha = Math.Sin(omega) / (2 * filter.CenterHz / filter.BandwidthHz);
                var z = Complex.FromPolarCoordinates(1, -2 * Math.PI * bin.FrequencyHz / spectrum.SampleRateHz);
                var numerator = 1 - 2 * Math.Cos(omega) * z + z * z;
                var denominator = 1 + alpha - 2 * Math.Cos(omega) * z + (1 - alpha) * z * z;
                var floor = Math.Pow(10, -filter.AttenuationDb / 20);
                gain *= (floor + (1 - floor) * numerator / denominator).Magnitude;
            }
            return new SimulatedFrequencyBin(bin.FrequencyHz, bin.Amplitude, bin.Amplitude * gain, gain);
        }).ToImmutableArray();
    }
}
