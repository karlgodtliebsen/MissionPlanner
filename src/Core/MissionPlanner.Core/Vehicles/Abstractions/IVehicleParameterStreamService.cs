using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>
/// Service for streaming all parameters from a vehicle with progress tracking.
/// </summary>
public interface IVehicleParameterStreamService
{
    /// <summary>
    /// Requests and streams parameters from a vehicle with retry logic.
    /// Automatically retries if parameters are missing or incomplete.
    /// </summary>
    /// <param name="vehicleId">The vehicle ID.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="maxRetries">Maximum number of retry attempts for missing parameters.</param>
    /// <param name="timeout">Maximum time to wait for all parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing all parameters or error information.</returns>
    Task<ParameterStreamResult> StreamAllParametersWithRetryAsync(VehicleId vehicleId, IProgress<ParameterStreamProgress>? progress = null, int maxRetries = 3, TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}
