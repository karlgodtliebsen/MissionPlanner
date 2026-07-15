namespace MissionPlanner.Core.Missions;

public sealed class Mission
{
    private readonly List<MissionItem> items = [];

    public Mission(MissionId id, string name, MissionPlanType type = MissionPlanType.FlightMission)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Mission name is required.", nameof(name));
        Id = id;
        Name = name.Trim();
        Type = type;
    }

    public MissionId Id { get; }
    public string Name { get; private set; }
    public MissionPlanType Type { get; }
    public int Revision { get; private set; }
    public IReadOnlyList<MissionItem> Items => items;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Mission name is required.", nameof(name));
        Name = name.Trim(); Revision++;
    }

    public void Add(MissionItem item) { items.Add(WithSequence(item, checked((ushort)items.Count))); Revision++; }
    public void Insert(int index, MissionItem item) { items.Insert(index, item); Resequence(); Revision++; }
    public bool Remove(MissionItemId id) { var n=items.RemoveAll(x=>x.Id==id); if(n>0){Resequence();Revision++;} return n>0; }
    public void Move(MissionItemId id, int destinationIndex)
    {
        var index=items.FindIndex(x=>x.Id==id); if(index<0) throw new KeyNotFoundException($"Mission item {id} was not found.");
        var item=items[index]; items.RemoveAt(index); items.Insert(destinationIndex,item); Resequence(); Revision++;
    }
    public void Replace(MissionItemId id, MissionItem replacement)
    {
        var index=items.FindIndex(x=>x.Id==id); if(index<0) throw new KeyNotFoundException($"Mission item {id} was not found.");
        items[index]=WithSequence(replacement,(ushort)index); Revision++;
    }

    private void Resequence(){ for(var i=0;i<items.Count;i++) items[i]=WithSequence(items[i],checked((ushort)i)); }
    private static MissionItem WithSequence(MissionItem item, ushort sequence) => item switch
    {
        WaypointMissionItem x => x with { Sequence=sequence }, TakeoffMissionItem x => x with { Sequence=sequence },
        LandMissionItem x => x with { Sequence=sequence }, ReturnToLaunchMissionItem x => x with { Sequence=sequence },
        ChangeSpeedMissionItem x => x with { Sequence=sequence }, LoiterMissionItem x => x with { Sequence=sequence },
        _ => throw new NotSupportedException(item.GetType().Name)
    };
}
