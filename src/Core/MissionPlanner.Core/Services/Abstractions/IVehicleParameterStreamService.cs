using MissionPlanner.Core.Models;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Services.Abstractions;

/// <summary>
/// Service for streaming all parameters from a vehicle with progress tracking.
/// </summary>
public interface IVehicleParameterStreamService
{
    /// <summary>
    /// Requests and streams all parameters from a vehicle.
    /// </summary>
    /// <param name="vehicleId">The vehicle ID.</param>
    /// <param name="progress">Optional progress reporter (reports percentage 0-100).</param>
    /// <param name="onParameterReceived">Optional callback invoked for each parameter received.</param>
    /// <param name="timeout">Maximum time to wait for all parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing all parameters or error information.</returns>
    Task<ParameterStreamResult> StreamAllParametersAsync(VehicleId vehicleId, IProgress<ParameterStreamProgress>? progress = null, Action<VehicleParameter>? onParameterReceived = null, TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);

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

/// <summary>
/// Progress information for parameter streaming.
/// </summary>
/// <param name="ReceivedCount">Number of parameters received so far.</param>
/// <param name="TotalCount">Total number of parameters (0 if not yet known).</param>
/// <param name="PercentComplete">Percentage complete (0-100).</param>
/// <param name="IsComplete">Whether all parameters have been received.</param>
public sealed record ParameterStreamProgress(int ReceivedCount, int TotalCount, int PercentComplete, bool IsComplete);

/// <summary>
/// Result of a parameter streaming operation.
/// </summary>
public sealed class ParameterStreamResult
{
    public bool Success { get; init; }
    public IReadOnlyDictionary<string, VehicleParameter> Parameters { get; init; } = new Dictionary<string, VehicleParameter>();
    public int TotalCount { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Creates a success result for a parameter streaming operation.
    /// </summary>
    /// <param name="parameters">The parameters received from the vehicle.</param>
    /// <param name="totalCount">The total number of parameters expected.</param>
    /// <param name="duration">The duration of the operation.</param>
    /// <returns>A <see cref="ParameterStreamResult"/> representing the success.</returns>
    public static ParameterStreamResult CreateSuccess(IReadOnlyDictionary<string, VehicleParameter> parameters, int totalCount, TimeSpan duration)
    {
        return new ParameterStreamResult { Success = true, Parameters = parameters, TotalCount = totalCount, Duration = duration };
    }

    /// <summary>
    /// Creates a failure result for a parameter streaming operation.
    /// </summary>
    /// <param name="errorMessage">The error message describing the failure.</param>
    /// <param name="duration">The duration of the operation before failure.</param>
    /// <returns>A <see cref="ParameterStreamResult"/> representing the failure.</returns>
    public static ParameterStreamResult CreateFailure(string errorMessage, TimeSpan duration)
    {
        return new ParameterStreamResult { Success = false, ErrorMessage = errorMessage, Duration = duration };
    }
}
