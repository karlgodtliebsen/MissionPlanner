namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Represents the various mission commands that can be issued in a mission plan.
/// </summary>
public enum MissionCommand : ushort
{
    /// <summary>
    /// Provides the public API for Waypoint.
    /// </summary>
    Waypoint = 16,
    /// <summary>
    /// Provides the public API for LoiterUnlimited.
    /// </summary>
    LoiterUnlimited = 17,
    /// <summary>
    /// Provides the public API for LoiterTurns.
    /// </summary>
    LoiterTurns = 18,
    /// <summary>
    /// Provides the public API for LoiterTime.
    /// </summary>
    LoiterTime = 19,
    /// <summary>
    /// Provides the public API for ReturnToLaunch.
    /// </summary>
    ReturnToLaunch = 20,
    /// <summary>
    /// Provides the public API for Land.
    /// </summary>
    Land = 21,
    /// <summary>
    /// Provides the public API for Takeoff.
    /// </summary>
    Takeoff = 22,
    /// <summary>Follows a spline path through the waypoint.</summary>
    SplineWaypoint = 82,
    /// <summary>Jumps mission execution to another sequence.</summary>
    Jump = 177,
    /// <summary>
    /// Provides the public API for ChangeSpeed.
    /// </summary>
    ChangeSpeed = 178,
    /// <summary>Points a region of interest at a geographic location.</summary>
    SetRoiLocation = 195,
    /// <summary>Legacy region-of-interest command retained for compatible decoding.</summary>
    SetRoi = 201,
    /// <summary>
    /// Provides the public API for SetRelay.
    /// </summary>
    SetRelay = 181,
    /// <summary>
    /// Provides the public API for SetServo.
    /// </summary>
    SetServo = 183
}
