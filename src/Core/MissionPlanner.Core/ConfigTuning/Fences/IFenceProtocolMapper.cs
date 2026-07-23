using MissionPlanner.MavLink.Missions;

namespace MissionPlanner.Core.ConfigTuning.Fences;

/// <summary>Maps dedicated fence geometry to and from typed MAVLink mission items.</summary>
public interface IFenceProtocolMapper
{
    /// <summary>Converts a fence plan to contiguous MAVLink fence items.</summary>
    /// <param name="plan">The fence plan.</param>
    /// <returns>The protocol items.</returns>
    IReadOnlyList<MavLinkMissionItem> ToProtocol(FencePlan plan);

    /// <summary>Converts MAVLink fence items to dedicated fence geometry.</summary>
    /// <param name="items">The protocol items.</param>
    /// <returns>The parsed plan and protocol-shape errors.</returns>
    FenceProtocolParseResult FromProtocol(IReadOnlyList<MavLinkMissionItem> items);
}
