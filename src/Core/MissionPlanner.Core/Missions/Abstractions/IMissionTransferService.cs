using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Transfer;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Missions.Abstractions;

/// <summary>
/// Defines the contract for a service that handles mission transfers to and from vehicles.
/// </summary>
public interface IMissionTransferService
{
    Task<MissionUploadResult> UploadAsync(VehicleId vehicleId, Mission mission, IProgress<MissionUploadProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<MissionDownloadResult> DownloadAsync(VehicleId vehicleId, MissionPlanType missionType = MissionPlanType.FlightMission, CancellationToken cancellationToken = default);
    Task ClearAsync(VehicleId vehicleId, MissionPlanType missionType = MissionPlanType.FlightMission, CancellationToken cancellationToken = default);
}
