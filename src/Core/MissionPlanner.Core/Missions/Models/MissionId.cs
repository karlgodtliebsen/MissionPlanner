namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Provides the public API for MissionId.
/// </summary>
public readonly record struct MissionId(Guid Value)
{
    /// <summary>
    /// Provides the public API for New.
    /// </summary>
    public static MissionId New()
    {
        return new MissionId(Guid.NewGuid());
    }

    /// <summary>
    /// Provides the public API for ToString.
    /// </summary>
    public override string ToString()
    {
        return Value.ToString("N");
    }
}
