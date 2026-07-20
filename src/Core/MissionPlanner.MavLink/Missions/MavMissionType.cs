namespace MissionPlanner.MavLink.Missions;

/// <summary>
/// Provides the public API for MavMissionType.
/// </summary>
/// <summary>
/// Provides the public API for Fence.
/// </summary>
public enum MavMissionType : byte
{
    /// <summary>A flight mission.</summary>
    Mission = 0,
    /// <summary>A geofence mission.</summary>
    Fence = 1,
    /// <summary>A rally-point mission.</summary>
    Rally = 2
}
