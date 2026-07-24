namespace MissionPlanner.Core.ConfigTuning.Fences;

/// <summary>Reports conversion from MAVLink fence mission items.</summary>
/// <param name="Plan">The converted plan.</param>
/// <param name="Errors">Protocol-shape errors encountered during conversion.</param>
public sealed record FenceProtocolParseResult(FencePlan Plan, IReadOnlyList<string> Errors)
{
    /// <summary>Gets whether all protocol items were converted.</summary>
    public bool Success => Errors.Count == 0;
}
