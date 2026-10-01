using MissionPlanner.Analysis.Frequency;

namespace MissionPlanner.Core.Analysis;

/// <summary>Peak location relative to nominal static filter regions.</summary>
/// <param name="Peak">Measured peak evidence.</param>
/// <param name="InsideStaticBand">Whether a supported nominal band contains the frequency.</param>
/// <param name="Assessment">Qualified static coverage description.</param>
public sealed record NotchPeakCoverage(SpectralPeak Peak, bool InsideStaticBand, string Assessment);