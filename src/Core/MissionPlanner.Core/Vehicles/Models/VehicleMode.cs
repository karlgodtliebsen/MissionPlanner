namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Represents the mode of a vehicle.
/// </summary>
public enum VehicleMode
{
    /// <summary>
    /// Provides the public API for Unknown.
    /// </summary>
    Unknown = 0,
    /// <summary>
    /// Provides the public API for Stabilize.
    /// </summary>
    Stabilize,
    /// <summary>
    /// Provides the public API for AltHold.
    /// </summary>
    AltHold,
    /// <summary>
    /// Provides the public API for Loiter.
    /// </summary>
    Loiter,
    /// <summary>
    /// Provides the public API for Guided.
    /// </summary>
    Guided,
    /// <summary>
    /// Provides the public API for Rtl.
    /// </summary>
    Rtl,
    /// <summary>
    /// Provides the public API for Land.
    /// </summary>
    Land
}
