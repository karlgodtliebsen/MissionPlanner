using System.Collections.Immutable;

namespace MissionPlanner.Analysis.Vibration;

/// <summary>Localized amplification during a speed sweep, not a structural defect diagnosis.</summary>
/// <param name="MinimumFrequencyHz">Lower edge of the candidate band.</param>
/// <param name="MaximumFrequencyHz">Upper edge of the candidate band.</param>
/// <param name="PeakFrequencyHz">Frequency at the largest measured amplitude.</param>
/// <param name="RelativeAmplification">Peak amplitude divided by median out-of-band amplitude.</param>
/// <param name="Confidence">Heuristic strength, not a calibrated probability.</param>
/// <param name="Evidence">All paired sweep measurements including the baseline.</param>
public sealed record ResonanceCandidate(double MinimumFrequencyHz, double MaximumFrequencyHz, double PeakFrequencyHz,
    double RelativeAmplification, double Confidence, ImmutableArray<MotorFrequencyObservation> Evidence);

/// <summary>Looks for localized amplitude amplification with measured excitation on both sides.</summary>
public sealed class ResonanceDetector
{
    /// <summary>Finds the strongest candidate in a correlated speed sweep.</summary>
    /// <param name="correlation">Time-varying RPM evidence.</param>
    /// <param name="bandwidthHz">Positive candidate band width.</param>
    /// <param name="minimumAmplification">Required peak-to-baseline amplitude ratio, greater than one.</param>
    /// <returns>A candidate only when both frequency flanks have baseline evidence.</returns>
    public ResonanceCandidate? Detect(MotorFrequencyCorrelation correlation, double bandwidthHz, double minimumAmplification = 3)
    {
        ArgumentNullException.ThrowIfNull(correlation);
        if (!double.IsFinite(bandwidthHz) || bandwidthHz <= 0 || !double.IsFinite(minimumAmplification) || minimumAmplification <= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(bandwidthHz));
        }
        if (correlation.Correlation is not >= 0.7 || correlation.Evidence.Length < 5)
        {
            return null;
        }
        var peak = correlation.Evidence.MaxBy(o => o.Amplitude)!;
        var lower = peak.ObservedFrequencyHz - bandwidthHz / 2;
        var upper = peak.ObservedFrequencyHz + bandwidthHz / 2;
        var evidence = correlation.Evidence;
        if (!evidence.Any(o => o.ObservedFrequencyHz < lower) || !evidence.Any(o => o.ObservedFrequencyHz > upper))
        {
            return null;
        }
        var outside = evidence.Where(o => o.ObservedFrequencyHz < lower || o.ObservedFrequencyHz > upper)
            .Select(o => o.Amplitude).Order().ToArray();
        var middle = outside.Length / 2;
        var baseline = outside.Length % 2 == 0 ? outside[middle - 1] / 2 + outside[middle] / 2 : outside[middle];
        if (baseline <= 0 || peak.Amplitude / baseline < minimumAmplification)
        {
            return null;
        }
        var ratio = peak.Amplitude / baseline;
        return new ResonanceCandidate(lower, upper, peak.ObservedFrequencyHz, ratio,
            Math.Clamp((1 - 1 / ratio) * correlation.Correlation.Value, 0, 1), evidence);
    }
}
