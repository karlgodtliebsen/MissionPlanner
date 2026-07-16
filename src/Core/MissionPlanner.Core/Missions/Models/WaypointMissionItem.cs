namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Represents a waypoint mission item.
/// </summary>
/// <param name="Id">The unique identifier of the mission item.</param>
/// <param name="Sequence">The sequence number of the mission item.</param>
/// <param name="Position">The geographical position of the waypoint.</param>
/// <param name="Altitude">The altitude of the waypoint.</param>
/// <param name="HoldTime">The time to hold at the waypoint.</param>
/// <param name="AcceptanceRadiusMeters">The acceptance radius in meters.</param>
/// <param name="PassRadiusMeters">The pass radius in meters.</param>
/// <param name="DesiredYawDegrees">The desired yaw in degrees.</param>
/// <param name="AutoContinue">Indicates whether to automatically continue to the next waypoint.</param>
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
    /// <summary>
    /// Gets the mission command for this waypoint.
    /// </summary>
    public override MissionCommand Command => MissionCommand.Waypoint;

    /// <summary>
    /// Gets the mission frame for this waypoint.
    /// </summary>
    public override MissionFrame Frame => Altitude.Reference.ToFrame();
}
