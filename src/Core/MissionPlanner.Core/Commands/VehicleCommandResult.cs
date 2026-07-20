namespace MissionPlanner.Core.Commands;

/// <summary>
/// Represents the result of a vehicle command. 
/// </summary>
public enum VehicleCommandResult
{
    /// <summary>
    /// Provides the public API for Accepted.
    /// </summary>
    Accepted = 0,
    /// <summary>
    /// Provides the public API for TemporarilyRejected.
    /// </summary>
    TemporarilyRejected = 1,
    /// <summary>
    /// Provides the public API for Denied.
    /// </summary>
    Denied = 2,
    /// <summary>
    /// Provides the public API for Unsupported.
    /// </summary>
    Unsupported = 3,
    /// <summary>
    /// Provides the public API for VehicleNotFound.
    /// </summary>
    VehicleNotFound = 4,
    /// <summary>
    /// Provides the public API for Failed.
    /// </summary>
    Failed = 5,
    /// <summary>
    /// Provides the public API for Timeout.
    /// </summary>
    Timeout = 100
}
