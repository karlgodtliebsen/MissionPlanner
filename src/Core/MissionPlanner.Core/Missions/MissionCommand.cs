namespace MissionPlanner.Core.Missions;

public enum MissionCommand : ushort
{
    NavigateWaypoint = 16,
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
