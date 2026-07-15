namespace MissionPlanner.Core.Missions;

public readonly record struct MissionId(Guid Value)
{
    public static MissionId New()
    {
        return new MissionId(Guid.NewGuid());
    }

    public override string ToString()
    {
        return Value.ToString("N");
    }
}
