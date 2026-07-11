using MissionPlanner.Core.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Services.Abstractions;

/// <summary>
/// Service for accessing parameter metadata for vehicles.
/// Automatically determines vehicle type and retrieves appropriate metadata.
/// </summary>
public interface IVehicleParameterMetadataService
{
    /// <summary>
    /// Gets metadata for a specific parameter on a vehicle.
    /// </summary>
    /// <param name="vehicleId">The vehicle ID.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parameter metadata, or null if not found.</returns>
    Task<ParameterMetadata?> GetMetadataAsync(VehicleId vehicleId, string parameterName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata for a specific parameter by vehicle type.
    /// </summary>
    /// <param name="vehicleType">The vehicle type.</param>
    /// <param name="parameterName">The parameter name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parameter metadata, or null if not found.</returns>
    Task<ParameterMetadata?> GetMetadataAsync(VehicleType vehicleType, string parameterName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all metadata for a vehicle.
    /// </summary>
    /// <param name="vehicleId">The vehicle ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of parameter name to metadata.</returns>
    Task<IReadOnlyDictionary<string, ParameterMetadata>> GetAllMetadataAsync(VehicleId vehicleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all metadata for a vehicle type.
    /// </summary>
    /// <param name="vehicleType">The vehicle type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of parameter name to metadata.</returns>
    Task<IReadOnlyDictionary<string, ParameterMetadata>> GetAllMetadataAsync(VehicleType vehicleType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a refresh of the metadata cache for a vehicle type.
    /// </summary>
    /// <param name="vehicleType">The vehicle type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RefreshMetadataAsync(VehicleType vehicleType, CancellationToken cancellationToken = default);
}
