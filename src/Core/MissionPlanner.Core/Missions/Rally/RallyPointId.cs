namespace MissionPlanner.Core.Missions.Rally;

/// <summary>Stable local rally-point identity.</summary>
public readonly record struct RallyPointId(Guid Value)
{ /// <summary>Creates an identity.</summary>
    public static RallyPointId New()
    {
        return new(Guid.NewGuid());
    }
}
