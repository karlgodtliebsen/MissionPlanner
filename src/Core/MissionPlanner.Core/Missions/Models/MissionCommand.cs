namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Represents the various mission commands that can be issued in a mission plan.
/// </summary>
public enum MissionCommand : ushort
{
    Waypoint = 16,
    LoiterUnlimited = 17,
    LoiterTurns = 18,
    LoiterTime = 19,
    ReturnToLaunch = 20,
    Land = 21,
    Takeoff = 22,
    ChangeSpeed = 178,
    SetRelay = 181,
    SetServo = 183
}
