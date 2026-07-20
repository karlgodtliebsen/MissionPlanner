namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Provides the public API for MissionExecutionState.
/// </summary>
/// <param name="MissionType">The MissionType value.</param>
/// <param name="CurrentSequence">The CurrentSequence value.</param>
/// <param name="LastReachedSequence">The LastReachedSequence value.</param>
/// <param name="TotalItems">The TotalItems value.</param>
/// <param name="Status">The Status value.</param>
/// <param name="LastUpdatedAt">The LastUpdatedAt value.</param>
public sealed record MissionExecutionState(
    MissionPlanType MissionType,
    ushort? CurrentSequence,
    ushort? LastReachedSequence,
    ushort? TotalItems,
    MissionExecutionStatus Status,
    DateTimeOffset? LastUpdatedAt)
{
    /// <summary>
    /// Provides the public API for Empty.
    /// </summary>
    public static MissionExecutionState Empty { get; } =
        new(MissionPlanType.FlightMission, null, null, null, MissionExecutionStatus.Unknown, null);
}
