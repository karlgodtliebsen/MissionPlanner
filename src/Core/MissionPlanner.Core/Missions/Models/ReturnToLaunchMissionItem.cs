namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Provides the public API for ReturnToLaunchMissionItem.
/// </summary>
public sealed record ReturnToLaunchMissionItem(MissionItemId Id, ushort Sequence, bool AutoContinue = true)
    : MissionItem(Id, Sequence, AutoContinue)
{
    /// <summary>
    /// Provides the public API for Command.
    /// </summary>
    public override MissionCommand Command => MissionCommand.ReturnToLaunch;
    /// <summary>
    /// Provides the public API for Frame.
    /// </summary>
    public override MissionFrame Frame => MissionFrame.GlobalRelativeAltitude;
}
