namespace MissionPlanner.Core.Missions;

public abstract record MissionItem(MissionItemId Id, ushort Sequence, bool AutoContinue = true)
{
    public abstract MissionCommand Command { get; }
    public abstract MissionFrame Frame { get; }
}
