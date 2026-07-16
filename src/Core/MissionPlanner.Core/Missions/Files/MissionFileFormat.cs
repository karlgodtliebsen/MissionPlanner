namespace MissionPlanner.Core.Missions.Files;

/// <summary>
/// Supported on-disk mission file formats.
/// </summary>
public enum MissionFileFormat
{
    /// <summary>QGC WPL 110 tab-separated text (.waypoints, .txt) — compatible with the classic MissionPlanner.</summary>
    QgcWpl = 0,

    /// <summary>JSON mission document (.mission).</summary>
    MissionJson = 1
}
