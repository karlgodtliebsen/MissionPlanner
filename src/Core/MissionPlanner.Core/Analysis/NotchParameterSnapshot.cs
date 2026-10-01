using System.Collections.Immutable;
using MissionPlanner.Analysis.Frequency;

namespace MissionPlanner.Core.Analysis;

/// <summary>Frozen filter configuration; it cannot write to a controller.</summary>
/// <param name="Source">Connected or saved source identity.</param>
/// <param name="Values">Actual values and descriptions.</param>
/// <param name="StaticFilters">Static harmonic bands that can be modeled.</param>
/// <param name="Limitations">Unsupported/dynamic/missing configuration information.</param>
public sealed record NotchParameterSnapshot(string Source, ImmutableArray<NotchParameterValue> Values,
    ImmutableArray<NotchFilter> StaticFilters, ImmutableArray<string> Limitations);