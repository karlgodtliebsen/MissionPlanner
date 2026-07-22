using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Provides the previous and current active-vehicle snapshots for a context change.
/// </summary>
public sealed class ActiveVehicleChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveVehicleChangedEventArgs"/> class.
    /// </summary>
    /// <param name="previous">The snapshot before the change.</param>
    /// <param name="current">The snapshot after the change.</param>
    public ActiveVehicleChangedEventArgs(ActiveVehicleSnapshot previous, ActiveVehicleSnapshot current)
    {
        Previous = previous;
        Current = current;
    }

    /// <summary>
    /// Gets the snapshot before the change.
    /// </summary>
    public ActiveVehicleSnapshot Previous { get; }

    /// <summary>
    /// Gets the snapshot after the change.
    /// </summary>
    public ActiveVehicleSnapshot Current { get; }
}
