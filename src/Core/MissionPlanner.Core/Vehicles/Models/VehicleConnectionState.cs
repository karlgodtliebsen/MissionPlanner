namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Represents the connection state of a vehicle.
/// </summary>
public enum VehicleConnectionState
{
    /// <summary>
    /// Provides the public API for Unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Provides the public API for Online.
    /// </summary>
    Online = 1,

    /// <summary>
    /// Provides the public API for Stale.
    /// </summary>
    Stale = 2,

    /// <summary>
    /// Provides the public API for Degraded.
    /// </summary>
    Degraded = 3,

    /// <summary>
    /// Provides the public API for Offline.
    /// </summary>
    Offline = 4
}
