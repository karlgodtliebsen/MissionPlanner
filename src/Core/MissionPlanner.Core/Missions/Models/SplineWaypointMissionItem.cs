namespace MissionPlanner.Core.Missions.Models;

/// <summary>Represents a spline waypoint that ArduPilot approaches along a curved path.</summary>
/// <param name="Id">Unique mission-item identifier.</param>
/// <param name="Sequence">Mission sequence.</param>
/// <param name="Position">Waypoint position.</param>
/// <param name="Altitude">Waypoint altitude and reference.</param>
/// <param name="HoldTime">Time to hold at the waypoint.</param>
/// <param name="AutoContinue">Whether execution continues automatically.</param>
public sealed record SplineWaypointMissionItem(MissionItemId Id, ushort Sequence, GeoPosition Position,
    MissionAltitude Altitude, TimeSpan HoldTime, bool AutoContinue = true) : MissionItem(Id, Sequence, AutoContinue)
{
    /// <inheritdoc />
    public override MissionCommand Command => MissionCommand.SplineWaypoint;
    /// <inheritdoc />
    public override MissionFrame Frame => Altitude.Reference.ToFrame();
}
