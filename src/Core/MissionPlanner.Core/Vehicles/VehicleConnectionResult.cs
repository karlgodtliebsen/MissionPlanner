using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Result of a vehicle connection attempt.
/// </summary>
/// <param name="Success">Indicates if the connection was successful</param>
/// <param name="VehicleId">The connected vehicle's ID (null if failed)</param>
/// <param name="ConnectionSession">The vehicle connection session (null if failed)</param>
/// <param name="ErrorMessage">Error message if connection failed</param>
public record VehicleConnectionResult(bool Success, VehicleId? VehicleId, IVehicleConnectionSession? ConnectionSession, string? ErrorMessage = null);
