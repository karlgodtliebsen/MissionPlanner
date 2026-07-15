namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Represents a mission item in a mission plan.
/// </summary>
/// <param name="Id">The unique identifier of the mission item.</param>
/// <param name="Sequence">The sequence number of the mission item.</param>
/// <param name="AutoContinue">Indicates whether the mission item should automatically continue to the next item.</param>
public abstract record MissionItem(MissionItemId Id, ushort Sequence, bool AutoContinue = true)
{
    /// <summary>
    /// Gets the command associated with the mission item.
    /// </summary>
    public abstract MissionCommand Command { get; }

    /// <summary>
    /// Gets the frame of reference for the mission item.
    /// </summary>
    public abstract MissionFrame Frame { get; }
}
