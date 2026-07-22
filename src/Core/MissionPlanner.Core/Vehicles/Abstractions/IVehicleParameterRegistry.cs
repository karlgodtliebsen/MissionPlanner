using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>
/// Registry for storing and retrieving vehicle parameters.
/// </summary>
public interface IVehicleParameterRegistry
{
    /// <summary>Occurs after a parameter is stored or a vehicle's parameter set is cleared.</summary>
    event EventHandler<VehicleParameterChangedEventArgs>? Changed;

    /// <summary>
    /// Stores or updates a parameter for a vehicle.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle.</param>
    /// <param name="parameter">The parameter to store.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    void StoreParameter(VehicleId vehicleId, VehicleParameter parameter, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a specific parameter by name for a vehicle.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <returns>The parameter if found; otherwise, null.</returns>
    VehicleParameter? GetParameter(VehicleId vehicleId, string parameterName);

    /// <summary>
    /// Gets all parameters for a vehicle.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle.</param>
    /// <returns>A dictionary of parameters keyed by parameter name.</returns>
    IReadOnlyDictionary<string, VehicleParameter> GetAllParameters(VehicleId vehicleId);

    /// <summary>
    /// Gets the parameter count for a vehicle.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle.</param>
    /// <returns>The total number of parameters, or null if unknown.</returns>
    ushort? GetParameterCount(VehicleId vehicleId);

    /// <summary>
    /// Clears all parameters for a vehicle.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle.</param>
    void ClearParameters(VehicleId vehicleId);
}
