using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Missions.Rally;

/// <summary>Owns active-vehicle-scoped local and synchronized rally plans.</summary>
public interface IRallyConfigurationService
{
    /// <summary>Raised when a workspace changes.</summary>
    event Action? Changed;
    /// <summary>Gets one vehicle workspace.</summary>
    RallyPlanSnapshot GetSnapshot(VehicleId vehicleId);
    /// <summary>Replaces a local plan.</summary>
    RallyPlanSnapshot SetLocalPlan(VehicleId vehicleId, RallyPlan plan);
    /// <summary>Downloads rally points.</summary>
    Task<RallyOperationResult> DownloadAsync(VehicleId vehicleId, bool replaceLocal, CancellationToken cancellationToken = default);
    /// <summary>Uploads the local rally plan.</summary>
    Task<RallyOperationResult> UploadAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);
    /// <summary>Clears acknowledged vehicle rally points.</summary>
    Task<RallyOperationResult> ClearVehicleAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);
}