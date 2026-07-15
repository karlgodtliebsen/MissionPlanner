namespace MissionPlanner.Core.Missions;

public sealed record LandMissionItem(
    MissionItemId Id,
    ushort Sequence,
    GeoPosition Position,
    MissionAltitude Altitude,
    double? AbortAltitudeMeters = null,
    double? DesiredYawDegrees = null,
    bool AutoContinue = true)
    : MissionItem(Id, Sequence, AutoContinue)
{
    public override MissionCommand Command => MissionCommand.Land;
    public override MissionFrame Frame => Altitude.Reference.ToFrame();
}
