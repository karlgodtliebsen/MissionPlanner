namespace MissionPlanner.Core.ConfigTuning;

/// <summary>Reports protocol-neutral progress for a sequential parameter apply.</summary>
/// <param name="Index">The one-based target index.</param>
/// <param name="Total">The total number of targets.</param>
/// <param name="Name">The parameter name.</param>
/// <param name="Phase">The current phase.</param>
/// <param name="Message">A user-facing progress message.</param>
public sealed record ParameterApplyProgress(
    int Index,
    int Total,
    string Name,
    ParameterApplyPhase Phase,
    string Message);
