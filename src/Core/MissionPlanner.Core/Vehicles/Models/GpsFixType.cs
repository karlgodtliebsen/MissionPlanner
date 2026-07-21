namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Provides the public API for GpsFixType.
/// </summary>
public enum GpsFixType : byte
{
    /// <summary>
    /// Provides the public API for Unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Provides the public API for NoGps.
    /// </summary>
    NoGps = 1,

    /// <summary>
    /// Provides the public API for NoFix.
    /// </summary>
    NoFix = 2,

    /// <summary>
    /// Provides the public API for Fix2D.
    /// </summary>
    Fix2D = 3,

    /// <summary>
    /// Provides the public API for Fix3D.
    /// </summary>
    Fix3D = 4,

    /// <summary>
    /// Provides the public API for DifferentialGps.
    /// </summary>
    DifferentialGps = 5,

    /// <summary>
    /// Provides the public API for RtkFloat.
    /// </summary>
    RtkFloat = 6,

    /// <summary>
    /// Provides the public API for RtkFixed.
    /// </summary>
    RtkFixed = 7,

    /// <summary>
    /// Provides the public API for Static.
    /// </summary>
    Static = 8,

    /// <summary>
    /// Provides the public API for Ppp.
    /// </summary>
    Ppp = 9
}
