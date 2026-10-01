using System.Collections.Immutable;

namespace MissionPlanner.Core.Analysis;

/// <summary>Decoded analysis sources and explicit quality diagnostics.</summary>
/// <param name="Name">User-visible artifact name.</param>
/// <param name="Series">Selectable uniform segments.</param>
/// <param name="MotorSamples">RPM and output samples on the same timeline.</param>
/// <param name="Diagnostics">Rejected data and sampling limitations.</param>
public sealed record ImuLogData(string Name, ImmutableArray<ImuSampleSeries> Series,
    ImmutableArray<MotorLogSample> MotorSamples, ImmutableArray<string> Diagnostics);