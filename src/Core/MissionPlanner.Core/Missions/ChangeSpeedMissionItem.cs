namespace MissionPlanner.Core.Missions;

public sealed record ChangeSpeedMissionItem(
    MissionItemId Id,
    ushort Sequence,
    MissionSpeedType SpeedType,
    double SpeedMetersPerSecond,
    double? ThrottlePercent = null,
    bool AutoContinue = true)
    : MissionItem(Id, Sequence, AutoContinue)
{
    public override MissionCommand Command => MissionCommand.ChangeSpeed;
    public override MissionFrame Frame => MissionFrame.GlobalRelativeAltitude;
}
