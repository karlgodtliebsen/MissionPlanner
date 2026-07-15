namespace MissionPlanner.Core.Missions;

public readonly record struct MissionItemId(Guid Value)
{
    public static MissionItemId New()
    {
        return new MissionItemId(Guid.NewGuid());
    }
}
