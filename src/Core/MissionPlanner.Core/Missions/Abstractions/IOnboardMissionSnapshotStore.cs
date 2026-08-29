using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Missions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Missions.Abstractions;

/// <summary>Owns downloaded onboard mission identity and strict freshness evaluation.</summary>
public interface IOnboardMissionSnapshotStore
{
    event EventHandler? Changed;
    OnboardMissionSnapshot? Get(VehicleId vehicleId, MissionPlanType missionType = MissionPlanType.FlightMission);
    void Record(OnboardMissionSnapshot snapshot);
    void Invalidate(VehicleId vehicleId, MissionPlanType missionType = MissionPlanType.FlightMission);
    MissionSnapshotFreshness GetFreshness(VehicleState vehicleState, MissionPlanType missionType = MissionPlanType.FlightMission);
    bool TryGetCurrentItem(VehicleState vehicleState, out MavLinkMissionItem? item);
}
