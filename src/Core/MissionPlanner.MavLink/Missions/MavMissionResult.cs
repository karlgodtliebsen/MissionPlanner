namespace MissionPlanner.MavLink.Missions;

/// <summary>
/// Provides the public API for MavMissionResult.
/// </summary>
public enum MavMissionResult : byte
{
    /// <summary>
    /// Provides the public API for Accepted.
    /// </summary>
    Accepted = 0,
    /// <summary>
    /// Provides the public API for Error.
    /// </summary>
    Error = 1,
    /// <summary>
    /// Provides the public API for UnsupportedFrame.
    /// </summary>
    UnsupportedFrame = 2,
    /// <summary>
    /// Provides the public API for Unsupported.
    /// </summary>
    Unsupported = 3,
    /// <summary>
    /// Provides the public API for NoSpace.
    /// </summary>
    NoSpace = 4,
    /// <summary>
    /// Provides the public API for Invalid.
    /// </summary>
    Invalid = 5,
    /// <summary>
    /// Provides the public API for InvalidParam1.
    /// </summary>
    InvalidParam1 = 6,
    /// <summary>
    /// Provides the public API for InvalidParam2.
    /// </summary>
    InvalidParam2 = 7,
    /// <summary>
    /// Provides the public API for InvalidParam3.
    /// </summary>
    InvalidParam3 = 8,
    /// <summary>
    /// Provides the public API for InvalidParam4.
    /// </summary>
    InvalidParam4 = 9,
    /// <summary>
    /// Provides the public API for InvalidParam5X.
    /// </summary>
    InvalidParam5X = 10,
    /// <summary>
    /// Provides the public API for InvalidParam6Y.
    /// </summary>
    InvalidParam6Y = 11,
    /// <summary>
    /// Provides the public API for InvalidParam7.
    /// </summary>
    InvalidParam7 = 12,
    /// <summary>
    /// Provides the public API for InvalidSequence.
    /// </summary>
    InvalidSequence = 13,
    /// <summary>
    /// Provides the public API for Denied.
    /// </summary>
    Denied = 14,
    /// <summary>
    /// Provides the public API for OperationCancelled.
    /// </summary>
    OperationCancelled = 15
}
