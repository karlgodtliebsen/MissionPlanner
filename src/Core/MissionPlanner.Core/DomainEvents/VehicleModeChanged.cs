using MissionPlanner.Core.Models;
using MissionPlanner.Library.EventHub.Events;

namespace MissionPlanner.Core.DomainEvents;

/// <summary>
/// Event that is triggered when a vehicle's mode changes, which may indicate a change in the vehicle's operational state (e.g., from manual to autonomous mode).
/// </summary>
public class VehicleModeChanged : DomainEvent<VehicleModeChange>
{
    /// <inheritdoc />
    public VehicleModeChanged(VehicleModeChange data)
        : base("VehicleModeChanged", data)
    {
    }

    /// <summary>
    /// Gets the vehicle mode change associated with the domain event.
    /// </summary>
    public VehicleModeChange VehicleModeChange => (VehicleModeChange)Payload!;

    /// <summary>
    /// Gets the vehicle ID associated with the domain event.
    /// </summary>
    public VehicleId VehicleId => VehicleModeChange.VehicleId;

    /// <summary>
    /// Gets the new mode of the vehicle.
    /// </summary>
    public VehicleMode Mode => VehicleModeChange.Mode;

    /// <summary>
    /// Gets the timestamp when the mode change occurred.
    /// </summary>
    public DateTimeOffset ChangedAt => VehicleModeChange.ChangedAt;
}
