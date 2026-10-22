using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Missions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Missions.Abstractions;

/// <summary>Owns downloaded onboard mission identity and strict freshness evaluation.</summary>
public interface IOnboardMissionSnapshotStore
{
    /// <summary>Occurs when a snapshot is recorded or invalidated.</summary>
    event EventHandler? Changed;
    /// <summary>Gets the last mission snapshot taken from the vehicle, if any.</summary>
    OnboardMissionSnapshot? Get(VehicleId vehicleId, MissionPlanType missionType = MissionPlanType.FlightMission);
    /// <summary>Records a mission snapshot taken from the vehicle.</summary>
    void Record(OnboardMissionSnapshot snapshot);
    /// <summary>Discards the snapshot for a vehicle and mission type.</summary>
    void Invalidate(VehicleId vehicleId, MissionPlanType missionType = MissionPlanType.FlightMission);
    /// <summary>Determines whether the snapshot is known to match the mission onboard the vehicle.</summary>
    MissionSnapshotFreshness GetFreshness(VehicleState vehicleState, MissionPlanType missionType = MissionPlanType.FlightMission);
    /// <summary>Gets the mission item the vehicle currently executes when it matches the snapshot.</summary>
    bool TryGetCurrentItem(VehicleState vehicleState, out MavLinkMissionItem? item);
}
