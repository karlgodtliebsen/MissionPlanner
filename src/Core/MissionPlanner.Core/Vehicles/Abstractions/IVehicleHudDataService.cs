using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>
/// Service that provides HUD-specific vehicle data for display purposes.
/// Transforms detailed vehicle state into HUD-optimized format.
/// </summary>
public interface IVehicleHudDataService
{
    /// <summary>
    /// Gets the current HUD data for a specific vehicle.
    /// </summary>
    /// <param name="vehicleId">The unique identifier of the vehicle.</param>
    /// <returns>HUD data for the vehicle, or null if vehicle is not found or no data is available.</returns>
    VehicleHudData? GetHudData(VehicleId vehicleId);

    /// <summary>
    /// Observes HUD data updates for a specific vehicle.
    /// Emits a new VehicleHudData whenever the vehicle state changes.
    /// </summary>
    /// <param name="vehicleId">The unique identifier of the vehicle to observe.</param>
    /// <returns>An observable stream of HUD data updates.</returns>
    IObservable<VehicleHudData> ObserveHudData(VehicleId vehicleId);

    /// <summary>
    /// Gets HUD data for the primary/selected vehicle.
    /// This is a convenience method for single-vehicle scenarios.
    /// </summary>
    /// <returns>HUD data for the primary vehicle, or null if no vehicle is available.</returns>
    VehicleHudData? GetPrimaryVehicleHudData();

    /// <summary>
    /// Observes HUD data updates for the primary/selected vehicle.
    /// Automatically switches to tracking a new vehicle if the primary vehicle changes.
    /// </summary>
    /// <returns>An observable stream of HUD data updates for the primary vehicle.</returns>
    IObservable<VehicleHudData> ObservePrimaryVehicleHudData();
}
