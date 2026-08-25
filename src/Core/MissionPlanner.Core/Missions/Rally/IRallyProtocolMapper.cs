namespace MissionPlanner.Core.Missions.Rally;

/// <summary>Maps rally domain objects to the typed MAVLink mission protocol.</summary>
public interface IRallyProtocolMapper
{
    /// <summary>Maps an ordered plan to rally mission items.</summary>
    IReadOnlyList<MissionPlanner.MavLink.Missions.MavLinkMissionItem> ToProtocol(RallyPlan plan);
    /// <summary>Parses rally mission items.</summary>
    RallyPlan FromProtocol(IReadOnlyList<MissionPlanner.MavLink.Missions.MavLinkMissionItem> items);
}