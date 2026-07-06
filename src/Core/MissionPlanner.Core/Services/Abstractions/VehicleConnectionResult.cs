using MissionPlanner.Core.Models;

namespace MissionPlanner.Core.Services.Abstractions;

/// <summary>
/// Result of a vehicle connection attempt.
/// </summary>
/// <param name="Success">Indicates if the connection was successful</param>
/// <param name="VehicleId">The connected vehicle's ID (null if failed)</param>
/// <param name="ErrorMessage">Error message if connection failed</param>
public record VehicleConnectionResult(bool Success, VehicleId? VehicleId, string? ErrorMessage = null);
