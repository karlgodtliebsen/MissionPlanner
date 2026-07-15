namespace MissionPlanner.Core.Missions.Models;

public sealed record MissionExecutionState(
    MissionPlanType MissionType,
    ushort? CurrentSequence,
    ushort? LastReachedSequence,
    ushort? TotalItems,
    MissionExecutionStatus Status,
    DateTimeOffset? LastUpdatedAt)
{
    public static MissionExecutionState Empty { get; } =
        new(MissionPlanType.FlightMission, null, null, null, MissionExecutionStatus.Unknown, null);
}
