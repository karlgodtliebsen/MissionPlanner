namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Identifies named MAVLink protocol capabilities used by Mission Planner.
/// </summary>
[Flags]
public enum MavProtocolCapability : ulong
{
    /// <summary>No capability.</summary>
    None = 0,
    /// <summary>Supports float parameter encoding.</summary>
    ParamFloat = 1UL << 1,
    /// <summary>Supports union parameter encoding.</summary>
    ParamUnion = 1UL << 4,
    /// <summary>Supports MAVLink FTP.</summary>
    Ftp = 1UL << 5,
    /// <summary>Supports the typed mission fence protocol.</summary>
    MissionFence = 1UL << 14
}
