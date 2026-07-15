using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>
/// Service for managing vehicle parameters via MAVLink.
/// Provides methods to request, read, and set parameters on vehicles.
/// </summary>
public interface IVehicleParameterService
{
    /// <summary>
    /// Requests all parameters from the specified vehicle.
    /// The vehicle will respond with PARAM_VALUE messages for each parameter.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the request was sent successfully; otherwise, false.</returns>
    Task<bool> RequestParameterListAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a specific parameter by name from the vehicle.
    /// The vehicle will respond with a PARAM_VALUE message for the requested parameter.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle.</param>
    /// <param name="parameterName">The parameter name (max 16 characters).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the request was sent successfully; otherwise, false.</returns>
    Task<bool> RequestParameterAsync(VehicleId vehicleId, string parameterName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a specific parameter by index from the vehicle.
    /// The vehicle will respond with a PARAM_VALUE message for the requested parameter.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle.</param>
    /// <param name="parameterIndex">The parameter index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the request was sent successfully; otherwise, false.</returns>
    Task<bool> RequestParameterByIndexAsync(VehicleId vehicleId, ushort parameterIndex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a parameter value on the specified vehicle.
    /// The vehicle will respond with a PARAM_VALUE message confirming the new value.
    /// </summary>
    /// <param name="vehicleId">The ID of the vehicle.</param>
    /// <param name="parameterName">The parameter name (max 16 characters).</param>
    /// <param name="value">The parameter value to set.</param>
    /// <param name="paramType">The parameter type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the command was sent successfully; otherwise, false.</returns>
    Task<bool> SetParameterAsync(VehicleId vehicleId, string parameterName, float value, MavParamType paramType, CancellationToken cancellationToken = default);
}
