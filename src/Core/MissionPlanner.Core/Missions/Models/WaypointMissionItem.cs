namespace MissionPlanner.Core.Missions;

public sealed record WaypointMissionItem(
    MissionItemId Id,
    ushort Sequence,
    GeoPosition Position,
    MissionAltitude Altitude,
    TimeSpan HoldTime,
    double? AcceptanceRadiusMeters = null,
    double? PassRadiusMeters = null,
    double? DesiredYawDegrees = null,
    bool AutoContinue = true)
    : MissionItem(Id, Sequence, AutoContinue)
{
    public override MissionCommand Command => MissionCommand.NavigateWaypoint;
    public override MissionFrame Frame => Altitude.Reference.ToFrame();
}
