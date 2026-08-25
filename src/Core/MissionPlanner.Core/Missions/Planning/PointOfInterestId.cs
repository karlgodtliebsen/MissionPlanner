namespace MissionPlanner.Core.Missions.Planning;

/// <summary>
/// Stable local POI identity.
/// </summary>
public readonly record struct PointOfInterestId(Guid Value)
{ /// <summary>Creates an identity.</summary>
    public static PointOfInterestId New()
    {
        return new(Guid.NewGuid());
    }
}
