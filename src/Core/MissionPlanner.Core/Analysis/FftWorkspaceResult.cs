using System.Collections.Immutable;
using MissionPlanner.Analysis.Frequency;
using MissionPlanner.Analysis.Vibration;

namespace MissionPlanner.Core.Analysis;

/// <summary>Immutable analysis output consumed by presentation and exports.</summary>
/// <param name="Source">Uniform source segment.</param>
/// <param name="StartSeconds">Actual selected first sample time.</param>
/// <param name="Spectrum">Measured mean-amplitude spectrum.</param>
/// <param name="Spectrogram">Measured time-frequency frames relative to StartSeconds.</param>
/// <param name="Peaks">Peak observations.</param>
/// <param name="Harmonics">Harmonic candidates.</param>
/// <param name="Correlations">Actual RPM evidence paired to independently selected dominant peaks.</param>
/// <param name="Resonances">Localized amplification candidates.</param>
/// <param name="Comparison">Optional baseline differences on a common frequency grid.</param>
/// <param name="Coverage">Static filter-band assessment.</param>
/// <param name="CurrentSimulation">Generic current-static-configuration prediction.</param>
/// <param name="ProposedSimulation">Generic proposed-configuration prediction.</param>
/// <param name="Notes">Interpretation and source limitations.</param>
public sealed record FftWorkspaceResult(ImuSampleSeries Source, double StartSeconds, FrequencySpectrum Spectrum, Spectrogram Spectrogram,
    ImmutableArray<SpectralPeak> Peaks, ImmutableArray<HarmonicSeries> Harmonics,
    ImmutableArray<MotorFrequencyCorrelation> Correlations, ImmutableArray<ResonanceCandidate> Resonances,
    ImmutableArray<SpectrumDifference> Comparison, ImmutableArray<NotchPeakCoverage> Coverage,
    ImmutableArray<SimulatedFrequencyBin> CurrentSimulation, ImmutableArray<SimulatedFrequencyBin> ProposedSimulation,
    ImmutableArray<string> Notes)
{
    /// <summary>Exact captured interval and transform choices.</summary>
    public FftAnalysisRequest? Request { get; init; }
    /// <summary>Read-only parameter snapshot used by this completed result.</summary>
    public NotchParameterSnapshot? ParameterSnapshot { get; init; }
    /// <summary>Exact generic proposed filter configurations used by this result.</summary>
    public ImmutableArray<NotchFilter> ProposedFilters { get; init; } = [];
    /// <summary>Measured baseline and provenance used for comparison.</summary>
    public FftBaseline? Baseline { get; init; }
}