namespace MissionPlanner.Core.Missions;

public sealed record TakeoffMissionItem(
    MissionItemId Id,
    ushort Sequence,
    GeoPosition? Position,
    MissionAltitude Altitude,
    double? PitchDegrees = null,
    double? HeadingDegrees = null,
    bool AutoContinue = true)
    : MissionItem(Id, Sequence, AutoContinue)
{
    public override MissionCommand Command => MissionCommand.Takeoff;
    public override MissionFrame Frame => Altitude.Reference.ToFrame();
}
