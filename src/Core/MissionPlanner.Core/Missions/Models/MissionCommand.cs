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
    /// <summary>
    /// Provides the public API for ChangeSpeed.
    /// </summary>
    ChangeSpeed = 178,
    /// <summary>
    /// Provides the public API for SetRelay.
    /// </summary>
    SetRelay = 181,
    /// <summary>
    /// Provides the public API for SetServo.
    /// </summary>
    SetServo = 183
}
