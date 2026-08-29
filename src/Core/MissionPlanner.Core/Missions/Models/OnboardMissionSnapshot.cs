using MissionPlanner.MavLink.Missions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Missions.Models;

/// <summary>An immutable mission downloaded from one exact vehicle session.</summary>
public sealed record OnboardMissionSnapshot(
    VehicleId VehicleId,
    MissionPlanType MissionType,
    IReadOnlyList<MavLinkMissionItem> Items,
    uint? MissionId,
    DateTimeOffset RetrievedAt);
