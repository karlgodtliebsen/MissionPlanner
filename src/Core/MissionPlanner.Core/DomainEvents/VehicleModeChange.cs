using MissionPlanner.Core.Models;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Represents a change in the mode of a vehicle.
/// </summary>
/// <param name="VehicleId">The ID of the vehicle.</param>
/// <param name="Mode">The new mode of the vehicle.</param>
/// <param name="ChangedAt">The timestamp when the mode change occurred.</param>
public record VehicleModeChange(VehicleId VehicleId, VehicleMode Mode, DateTimeOffset ChangedAt);
