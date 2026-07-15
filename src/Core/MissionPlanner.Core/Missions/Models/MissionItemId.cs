namespace MissionPlanner.Core.Missions.Models;

public readonly record struct MissionItemId(Guid Value)
{
    public static MissionItemId New()
    {
        return new MissionItemId(Guid.NewGuid());
    }
}
