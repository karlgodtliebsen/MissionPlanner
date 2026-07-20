namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Provides the public API for MissionItemId.
/// </summary>
public readonly record struct MissionItemId(Guid Value)
{
    /// <summary>
    /// Provides the public API for New.
    /// </summary>
    public static MissionItemId New()
    {
        return new MissionItemId(Guid.NewGuid());
    }
}
