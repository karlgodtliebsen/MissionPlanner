using MissionPlanner.Core.ConfigTuning.Comparison;

namespace MissionPlanner.Core.ConfigTuning.Profiles;

/// <summary>Compatibility and value comparison for a profile against an edit session.</summary>
public sealed record ParameterProfileReview(
    ParameterProfile Profile,
    ParameterComparisonResult Comparison,
    IReadOnlyList<string> Warnings);
