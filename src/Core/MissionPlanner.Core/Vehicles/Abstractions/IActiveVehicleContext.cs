using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles.Abstractions;

/// <summary>
/// Exposes the selected vehicle and a cancellation boundary tied to its online connection lifetime.
/// </summary>
public interface IActiveVehicleContext
{
    /// <summary>
    /// Gets the current immutable active-vehicle snapshot.
    /// </summary>
    ActiveVehicleSnapshot Current { get; }

    /// <summary>
    /// Gets the selected vehicle identifier, or <see langword="null"/> when no vehicle is selected.
    /// </summary>
    VehicleId? VehicleId { get; }

    /// <summary>
    /// Gets the latest immutable state for the selected vehicle.
    /// </summary>
    VehicleState? State { get; }

    /// <summary>
    /// Gets a value indicating whether the selected vehicle is online.
    /// </summary>
    bool IsOnline { get; }

    /// <summary>
    /// Gets a token that is cancelled when the active vehicle changes or becomes unavailable.
    /// </summary>
    CancellationToken ConnectionCancellationToken { get; }

    /// <summary>
    /// Occurs once whenever the effective active-vehicle snapshot changes.
    /// </summary>
    event EventHandler<ActiveVehicleChangedEventArgs>? Changed;
}
