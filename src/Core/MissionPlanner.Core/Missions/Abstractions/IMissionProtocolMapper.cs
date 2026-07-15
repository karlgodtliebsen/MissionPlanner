using MissionPlanner.Core.Missions.Models;
using MissionPlanner.MavLink.Missions;

namespace MissionPlanner.Core.Missions.Abstractions;

/// <summary>
/// Defines the contract for a service that maps mission items to their corresponding protocol representations.
/// </summary>
public interface IMissionProtocolMapper
{
    /// <summary>
    /// Maps a mission item to its corresponding protocol representation.
    /// </summary>
    /// <param name="item">The mission item to be mapped.</param>
    /// <param name="missionType">The type of the mission plan.</param>
    /// <returns>The protocol representation of the mission item.</returns>
    MavLinkMissionItem ToProtocol(MissionItem item, MissionPlanType missionType);

    /// <summary>
    /// Maps a protocol representation of a mission item back to its corresponding mission item.
    /// </summary>
    /// <param name="item">The protocol representation of the mission item.</param>
    /// <returns>The mission item corresponding to the protocol representation.</returns>
    MissionItem FromProtocol(MavLinkMissionItem item);
}
