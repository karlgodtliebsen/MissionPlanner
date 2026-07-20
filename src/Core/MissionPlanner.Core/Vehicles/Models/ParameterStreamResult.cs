using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Result of a parameter streaming operation.
/// </summary>
public sealed class ParameterStreamResult
{
    /// <summary>
    /// Provides the public API for Success.
    /// </summary>
    public bool Success { get; init; }
    /// <summary>
    /// Provides the public API for Parameters.
    /// </summary>
    public IReadOnlyDictionary<string, VehicleParameter> Parameters { get; init; } = new Dictionary<string, VehicleParameter>();
    /// <summary>
    /// Provides the public API for TotalCount.
    /// </summary>
    public int TotalCount { get; init; }
    /// <summary>
    /// Provides the public API for ErrorMessage.
    /// </summary>
    public string? ErrorMessage { get; init; }
    /// <summary>
    /// Provides the public API for Duration.
    /// </summary>
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
