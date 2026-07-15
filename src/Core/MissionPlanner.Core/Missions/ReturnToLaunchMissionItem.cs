namespace MissionPlanner.Core.Missions;

public sealed record ReturnToLaunchMissionItem(MissionItemId Id, ushort Sequence, bool AutoContinue = true)
    : MissionItem(Id, Sequence, AutoContinue)
{
    public override MissionCommand Command => MissionCommand.ReturnToLaunch;
    public override MissionFrame Frame => MissionFrame.GlobalRelativeAltitude;
}
