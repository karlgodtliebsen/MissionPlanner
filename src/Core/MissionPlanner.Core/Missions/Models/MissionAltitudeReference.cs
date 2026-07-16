namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Represents the reference for the altitude of a mission item.
/// </summary>
public enum MissionAltitudeReference
{
    /// <summary>
    /// The altitude is relative to the home position.
    /// </summary>
    Home,

    /// <summary>
    /// The altitude is relative to mean sea level.
    /// </summary>
    MeanSeaLevel,

    /// <summary>
    /// The altitude is relative to the terrain.
    /// </summary>
    Terrain
}
