using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Vehicles;

/// <summary>
/// Formats vehicle firmware identity for diagnostics and user interfaces.
/// </summary>
public static class VehicleFirmwareDisplayFormatter
{
    /// <summary>Formats a concise firmware family and version display value.</summary>
    /// <param name="identity">The identity to format.</param>
    /// <returns>A human-readable firmware identity.</returns>
    public static string Format(VehicleFirmwareIdentity identity)
    {
        var family = identity.Family.ToString();
        if (identity.FlightVersion is not { } version)
        {
            return $"{family} (version unknown)";
        }

        var suffix = version.ReleaseType switch
        {
            FirmwareReleaseType.Official => string.Empty,
            FirmwareReleaseType.Development => "-dev",
            FirmwareReleaseType.Alpha => "-alpha",
            FirmwareReleaseType.Beta => "-beta",
            FirmwareReleaseType.ReleaseCandidate => "-rc",
            _ => $"-{(byte)version.ReleaseType}"
        };
        return $"{family} {version.Major}.{version.Minor}.{version.Patch}{suffix}";
    }
}
