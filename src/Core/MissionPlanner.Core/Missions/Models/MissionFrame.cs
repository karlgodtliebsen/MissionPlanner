namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Provides the public API for MissionFrame.
/// </summary>
public enum MissionFrame : byte
{
    /// <summary>
    /// Provides the public API for Global.
    /// </summary>
    Global = 0,
    /// <summary>
    /// Provides the public API for GlobalRelativeAltitude.
    /// </summary>
    GlobalRelativeAltitude = 3,
    /// <summary>
    /// Provides the public API for GlobalTerrainAltitude.
    /// </summary>
    GlobalTerrainAltitude = 10
}
