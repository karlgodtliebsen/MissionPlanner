using System.Collections.Immutable;

namespace MissionPlanner.Analysis.Frequency;

/// <summary>Amplitude comparison at a common physical frequency.</summary>
/// <param name="FrequencyHz">Common comparison frequency.</param>
/// <param name="Baseline">Interpolated baseline amplitude.</param>
/// <param name="Current">Interpolated current amplitude.</param>
/// <param name="Change">Current minus baseline.</param>
/// <param name="Ratio">Current/baseline, null for a zero baseline.</param>
public sealed record SpectrumDifference(double FrequencyHz, double Baseline, double Current, double Change, double? Ratio);

/// <summary>Compares normalized amplitudes on a common frequency grid without matching array indexes.</summary>
public sealed class SpectrumComparer
{
    /// <summary>Interpolates both spectra on the coarser grid, limited to their shared Nyquist range.</summary>
    /// <param name="baseline">Reference spectrum with the same physical units as current.</param>
    /// <param name="current">Measured spectrum to compare.</param>
    /// <returns>Frequency-aligned amplitude differences; leakage can still vary with window length.</returns>
    public ImmutableArray<SpectrumDifference> Compare(FrequencySpectrum baseline, FrequencySpectrum current)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(current);
        if (baseline.Options.Window != current.Options.Window || baseline.Options.RemoveDc != current.Options.RemoveDc)
        {
            throw new ArgumentException("Use matching window and DC-removal settings for comparison.");
        }
        var step = Math.Max(baseline.ResolutionHz, current.ResolutionHz);
        var count = (int)Math.Floor(Math.Min(baseline.NyquistHz, current.NyquistHz) / step);
        var result = ImmutableArray.CreateBuilder<SpectrumDifference>(count + 1);
        for (var i = 0; i <= count; i++)
        {
            var hz = i * step;
            var a = Interpolate(baseline, hz);
            var b = Interpolate(current, hz);
            result.Add(new SpectrumDifference(hz, a, b, b - a, a > 0 ? b / a : null));
        }
        return result.MoveToImmutable();
    }

    private static double Interpolate(FrequencySpectrum spectrum, double hz)
    {
        var position = hz / spectrum.ResolutionHz;
        var index = Math.Min((int)position, spectrum.Bins.Length - 1);
        var next = Math.Min(index + 1, spectrum.Bins.Length - 1);
        return spectrum.Bins[index].Amplitude + (spectrum.Bins[next].Amplitude - spectrum.Bins[index].Amplitude) * (position - index);
    }
}
