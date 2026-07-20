namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Provides the public API for MissionSpeedType.
/// </summary>
/// <summary>
/// Provides the public API for Airspeed.
/// </summary>
public enum MissionSpeedType : byte
{
    /// <summary>Aircraft airspeed.</summary>
    Airspeed = 0,
    /// <summary>Vehicle ground speed.</summary>
    GroundSpeed = 1,
    /// <summary>Vertical climb speed.</summary>
    ClimbSpeed = 2,
    /// <summary>Vertical descent speed.</summary>
    DescentSpeed = 3
}
