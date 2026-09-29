using System.Collections.Immutable;

namespace MissionPlanner.Analysis.Frequency;

/// <summary>Thresholds in linear amplitude units.</summary>
public sealed record PeakDetectionOptions
{
    /// <summary>Minimum absolute amplitude required for a peak.</summary>
    public double MinimumAmplitude { get; init; } = 0.01;
    /// <summary>Minimum ratio to the median non-DC bin amplitude.</summary>
    public double NoiseFloorMultiplier { get; init; } = 5;
    /// <summary>Minimum fraction of the largest non-DC amplitude, between zero and one.</summary>
    public double RelativeThreshold { get; init; } = 0.05;
}

/// <summary>A local maximum with the numerical evidence used to select it.</summary>
/// <param name="BinIndex">Index in the source spectrum.</param>
/// <param name="FrequencyHz">Bin center; no sub-bin precision is implied.</param>
/// <param name="Amplitude">Measured peak amplitude.</param>
/// <param name="NoiseFloor">Median amplitude across non-DC bins.</param>
/// <param name="Threshold">Effective maximum of the configured thresholds.</param>
public sealed record SpectralPeak(int BinIndex, double FrequencyHz, double Amplitude, double NoiseFloor, double Threshold);

/// <summary>Finds local maxima above an explicit, reproducible amplitude threshold.</summary>
public sealed class PeakDetector
{
    /// <summary>Detects non-DC peaks, including a possible Nyquist peak.</summary>
    /// <param name="spectrum">Source spectrum.</param>
    /// <param name="options">Absolute, relative and median-noise thresholds.</param>
    /// <returns>Peaks ordered by ascending frequency.</returns>
    public ImmutableArray<SpectralPeak> Detect(FrequencySpectrum spectrum, PeakDetectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(spectrum);
        ArgumentNullException.ThrowIfNull(options);
        if (!double.IsFinite(options.MinimumAmplitude) || options.MinimumAmplitude < 0 ||
            !double.IsFinite(options.NoiseFloorMultiplier) || options.NoiseFloorMultiplier < 0 ||
            !double.IsFinite(options.RelativeThreshold) || options.RelativeThreshold < 0 || options.RelativeThreshold > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
        var amplitudes = spectrum.Bins.Skip(1).Select(bin => bin.Amplitude).Order().ToArray();
        var middle = amplitudes.Length / 2;
        var noise = amplitudes.Length % 2 == 0
            ? amplitudes[middle - 1] / 2 + amplitudes[middle] / 2
            : amplitudes[middle];
        var threshold = Math.Max(options.MinimumAmplitude,
            Math.Max(noise * options.NoiseFloorMultiplier, amplitudes[^1] * options.RelativeThreshold));
        var peaks = ImmutableArray.CreateBuilder<SpectralPeak>();
        for (var i = 1; i < spectrum.Bins.Length; i++)
        {
            var bin = spectrum.Bins[i];
            if (bin.Amplitude > threshold && bin.Amplitude > spectrum.Bins[i - 1].Amplitude &&
                (i == spectrum.Bins.Length - 1 || bin.Amplitude >= spectrum.Bins[i + 1].Amplitude))
            {
                peaks.Add(new SpectralPeak(i, bin.FrequencyHz, bin.Amplitude, noise, threshold));
            }
        }
        return peaks.ToImmutable();
    }
}
