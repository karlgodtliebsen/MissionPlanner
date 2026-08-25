namespace MissionPlanner.Core.Missions.Rally;

/// <summary>Versioned rally JSON codec.</summary>
public interface IRallyPlanFileCodec
{
    /// <summary>Serializes a rally plan.</summary>
    string Serialize(RallyPlan plan, DateTimeOffset createdAt);
    /// <summary>Parses a rally plan.</summary>
    RallyPlan Deserialize(string json);
}