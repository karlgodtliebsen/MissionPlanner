namespace MissionPlanner.Core.Missions.Models;

/// <summary>
/// Provides the public API for Mission.
/// </summary>
public sealed class Mission
{
    private readonly List<MissionItem> items = [];

    /// <summary>
    /// Provides the public API for Mission.
    /// </summary>
    public Mission(MissionId id, string name, MissionPlanType type = MissionPlanType.FlightMission)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Mission name is required.", nameof(name));
        }

        Id = id;
        Name = name.Trim();
        Type = type;
    }

    /// <summary>
    /// Provides the public API for Id.
    /// </summary>
    public MissionId Id { get; }
    /// <summary>
    /// Provides the public API for Name.
    /// </summary>
    public string Name { get; private set; }
    /// <summary>
    /// Provides the public API for Type.
    /// </summary>
    public MissionPlanType Type { get; }
    /// <summary>
    /// Provides the public API for Revision.
    /// </summary>
    public int Revision { get; private set; }
    /// <summary>
    /// Provides the public API for Items.
    /// </summary>
    public IReadOnlyList<MissionItem> Items => items;

    /// <summary>
    /// Provides the public API for Rename.
    /// </summary>
    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Mission name is required.", nameof(name));
        }

        Name = name.Trim();
        Revision++;
    }

    /// <summary>
    /// Provides the public API for Add.
    /// </summary>
    public void Add(MissionItem item)
    {
        items.Add(WithSequence(item, checked((ushort)items.Count)));
        Revision++;
    }

    /// <summary>
    /// Provides the public API for Insert.
    /// </summary>
    public void Insert(int index, MissionItem item)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, items.Count);

        items.Insert(index, item);
        Resequence();
        Revision++;
    }

    /// <summary>
    /// Provides the public API for Remove.
    /// </summary>
    public bool Remove(MissionItemId id)
    {
        var n = items.RemoveAll(x => x.Id == id);
        if (n > 0)
        {
            Resequence();
            Revision++;
        }

        return n > 0;
    }

    /// <summary>
    /// Provides the public API for Move.
    /// </summary>
    public void Move(MissionItemId id, int destinationIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(destinationIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(destinationIndex, items.Count);

        var index = items.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            throw new KeyNotFoundException($"Mission item {id} was not found.");
        }

        var item = items[index];
        items.RemoveAt(index);
        items.Insert(destinationIndex, item);
        Resequence();
        Revision++;
    }

    /// <summary>
    /// Provides the public API for Replace.
    /// </summary>
    public void Replace(MissionItemId id, MissionItem replacement)
    {
        var index = items.FindIndex(x => x.Id == id);
        if (index < 0)
        {
            throw new KeyNotFoundException($"Mission item {id} was not found.");
        }

        items[index] = WithSequence(replacement, (ushort)index);
        Revision++;
    }

    private void Resequence()
    {
        for (var i = 0; i < items.Count; i++)
        {
            items[i] = WithSequence(items[i], checked((ushort)i));
        }
    }

    private static MissionItem WithSequence(MissionItem item, ushort sequence)
    {
        return item switch
        {
            WaypointMissionItem x => x with { Sequence = sequence }, TakeoffMissionItem x => x with { Sequence = sequence },
            LandMissionItem x => x with { Sequence = sequence }, ReturnToLaunchMissionItem x => x with { Sequence = sequence },
            ChangeSpeedMissionItem x => x with { Sequence = sequence }, LoiterMissionItem x => x with { Sequence = sequence },
            var _ => throw new NotSupportedException(item.GetType().Name)
        };
    }
}
