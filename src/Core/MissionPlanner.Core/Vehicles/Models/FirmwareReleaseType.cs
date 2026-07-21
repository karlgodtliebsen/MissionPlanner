namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Identifies the release channel encoded in a MAVLink firmware version.
/// </summary>
public enum FirmwareReleaseType : byte
{
    /// <summary>A development build.</summary>
    Development = 0,
    /// <summary>An alpha build.</summary>
    Alpha = 64,
    /// <summary>A beta build.</summary>
    Beta = 128,
    /// <summary>A release-candidate build.</summary>
    ReleaseCandidate = 192,
    /// <summary>An official release.</summary>
    Official = 255
}
