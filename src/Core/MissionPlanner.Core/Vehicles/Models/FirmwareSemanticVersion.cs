namespace MissionPlanner.Core.Vehicles.Models;

/// <summary>
/// Represents the semantic flight-software version encoded by AUTOPILOT_VERSION.
/// </summary>
/// <param name="Major">The major version.</param>
/// <param name="Minor">The minor version.</param>
/// <param name="Patch">The patch version.</param>
/// <param name="ReleaseType">The firmware release channel.</param>
public sealed record FirmwareSemanticVersion(byte Major, byte Minor, byte Patch, FirmwareReleaseType ReleaseType)
{
    /// <summary>Decodes a packed MAVLink flight software version.</summary>
    /// <param name="packedVersion">The packed 32-bit version.</param>
    /// <returns>The decoded semantic version.</returns>
    public static FirmwareSemanticVersion FromPacked(uint packedVersion) => new(
        (byte)(packedVersion >> 24),
        (byte)(packedVersion >> 16),
        (byte)(packedVersion >> 8),
        (FirmwareReleaseType)packedVersion);
}
