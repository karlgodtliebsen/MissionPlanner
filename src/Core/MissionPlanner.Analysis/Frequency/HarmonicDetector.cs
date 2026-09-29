using System.Collections.Immutable;

namespace MissionPlanner.Analysis.Frequency;

/// <summary>One observed member of a candidate harmonic series.</summary>
/// <param name="Order">Integer multiple of the observed fundamental.</param>
/// <param name="Peak">Original measured peak.</param>
/// <param name="ExpectedFrequencyHz">Order times fundamental bin frequency.</param>
/// <param name="DifferenceHz">Signed observed minus expected frequency.</param>
/// <param name="ToleranceHz">Allowed difference including accumulated bin uncertainty.</param>
public sealed record HarmonicEvidence(int Order, SpectralPeak Peak, double ExpectedFrequencyHz, double DifferenceHz, double ToleranceHz);

/// <summary>Candidate relationship, not a mechanical diagnosis.</summary>
/// <param name="FundamentalHz">Observed first-order peak frequency; missing fundamentals are not inferred.</param>
/// <param name="Harmonics">Measured members and their frequency residuals.</param>
/// <param name="Confidence">Heuristic support in [0,1], not a calibrated probability.</param>
public sealed record HarmonicSeries(double FundamentalHz, ImmutableArray<HarmonicEvidence> Harmonics, double Confidence);

/// <summary>Finds approximate integer relationships between measured peaks.</summary>
public sealed class HarmonicDetector
{
    /// <summary>Returns candidates containing at least three measured orders, retaining ambiguous alternatives.</summary>
    /// <param name="peaks">Positive-frequency peaks from the same spectrum.</param>
    /// <param name="resolutionHz">Positive source bin spacing.</param>
    /// <param name="maximumOrder">Highest harmonic order to search, from 3 to 32.</param>
    /// <returns>Candidate series with immutable numerical evidence.</returns>
    public ImmutableArray<HarmonicSeries> Detect(ImmutableArray<SpectralPeak> peaks, double resolutionHz, int maximumOrder = 8)
    {
        if (!double.IsFinite(resolutionHz) || resolutionHz <= 0 || maximumOrder < 3 || maximumOrder > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(resolutionHz));
        }
        if (peaks.IsDefault || peaks.Any(p => p is null || !double.IsFinite(p.FrequencyHz) || p.FrequencyHz <= 0))
        {
            throw new ArgumentException("Peaks must have finite positive frequencies.", nameof(peaks));
        }
        var result = ImmutableArray.CreateBuilder<HarmonicSeries>();
        foreach (var fundamental in peaks.OrderBy(p => p.FrequencyHz))
        {
            var members = ImmutableArray.CreateBuilder<HarmonicEvidence>();
            members.Add(new HarmonicEvidence(1, fundamental, fundamental.FrequencyHz, 0, resolutionHz / 2));
            var usedBins = new HashSet<int> { fundamental.BinIndex };
            for (var order = 2; order <= maximumOrder; order++)
            {
                var expected = fundamental.FrequencyHz * order;
                // Both measured bins can be off by half a bin; fundamental error scales with order.
                var tolerance = Math.Min((order + 1) * resolutionHz / 2, fundamental.FrequencyHz * 0.1);
                var match = peaks.Where(p => !usedBins.Contains(p.BinIndex) && Math.Abs(p.FrequencyHz - expected) <= tolerance)
                    .OrderBy(p => Math.Abs(p.FrequencyHz - expected)).FirstOrDefault();
                if (match is not null)
                {
                    usedBins.Add(match.BinIndex);
                    members.Add(new HarmonicEvidence(order, match, expected, match.FrequencyHz - expected, tolerance));
                }
            }
            if (members.Count >= 3)
            {
                var fit = members.Skip(1).Average(m => Math.Max(0, 1 - Math.Abs(m.DifferenceHz) / m.ToleranceHz));
                var coverage = (double)members.Count / members[^1].Order;
                result.Add(new HarmonicSeries(fundamental.FrequencyHz, members.ToImmutable(), fit * coverage));
            }
        }
        return result.ToImmutable();
    }
}
