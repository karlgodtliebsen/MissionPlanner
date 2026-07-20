namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Provides the public API for MissionPlanType.
/// </summary>
/// <summary>
/// Provides the public API for FlightMission.
/// </summary>
public enum MissionPlanType : byte
{
    /// <summary>A flight mission.</summary>
    FlightMission = 0,
    /// <summary>A geofence plan.</summary>
    Geofence = 1,
    /// <summary>A rally-point plan.</summary>
    RallyPoints = 2
}
