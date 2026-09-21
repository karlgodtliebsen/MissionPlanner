using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Installation;

/// <summary>Identity policy for normal upgrades, separate from wrong-target recovery.</summary>
public static class FirmwareUpgradeVerification
{
    /// <summary>Checks firmware family, and after flash the exact version and original hardware identity.</summary>
    public static void Validate(VehicleFirmwareIdentity identity, FirmwareManifestEntry release,
        bool afterFlash, VehicleFirmwareIdentity? original = null) =>
        Validate(identity, SelectedFirmwareIdentity.FromRelease(release), afterFlash, original);

    /// <summary>Verifies embedded or catalogue expectations without inventing a catalogue entry for local firmware.</summary>
    public static void Validate(VehicleFirmwareIdentity identity, SelectedFirmwareIdentity release,
        bool afterFlash, VehicleFirmwareIdentity? original = null)
    {
        var family = release.VehicleType switch
        {
            FirmwareVehicleType.Copter or FirmwareVehicleType.Helicopter => FirmwareFamily.ArduCopter,
            FirmwareVehicleType.Plane => FirmwareFamily.ArduPlane,
            FirmwareVehicleType.Rover => FirmwareFamily.Rover,
            FirmwareVehicleType.Sub => FirmwareFamily.ArduSub,
            FirmwareVehicleType.AntennaTracker => FirmwareFamily.AntennaTracker,
            FirmwareVehicleType.Blimp => FirmwareFamily.Blimp,
            _ => FirmwareFamily.Unknown
        };
        if (!Version.TryParse(release.Version, out var expected))
        {
            throw new FirmwareCompatibilityException("The selected release has no verifiable semantic version.");
        }
        if (expected.Build < 0 || identity.Autopilot != 3 || family == FirmwareFamily.Unknown || identity.Family != family)
        {
            throw new FirmwareCompatibilityException("Running firmware family does not match the selected ArduPilot release. Use explicit recovery for a family/target change.");
        }
        // ArduPilot encodes APJ_BOARD_ID in AUTOPILOT_VERSION.board_version's upper word.
        var reportedBoard = identity.BoardVersion >> 16;
        if (reportedBoard != 0 && reportedBoard != release.BoardId)
        {
            throw new FirmwareCompatibilityException("Running controller board ID differs from the selected APJ target.");
        }
        if (!afterFlash)
        {
            return;
        }
        if ((release.VehicleType == FirmwareVehicleType.Helicopter && identity.MavType != 4) ||
            (release.VehicleType == FirmwareVehicleType.Copter && identity.MavType == 4))
        {
            throw new FirmwareVerificationException("Post-flash vehicle variant differs from the selected release.");
        }
        if (identity.FlightVersion is not { } version || version.Major != expected.Major ||
            version.Minor != expected.Minor || version.Patch != expected.Build ||
            (release.RequireOfficialRelease && version.ReleaseType != FirmwareReleaseType.Official))
        {
            throw new FirmwareVerificationException($"Post-flash version mismatch: expected {release.Version}, received {identity.FlightVersion}.");
        }
        if (original is not null &&
            ((original.HardwareUid is > 0 && identity.HardwareUid != original.HardwareUid) ||
             (!string.IsNullOrWhiteSpace(original.HardwareUid2) && identity.HardwareUid2 != original.HardwareUid2)))
        {
            throw new FirmwareVerificationException("The returning controller hardware UID differs from the original.");
        }
        var actual = identity.FlightGitHash;
        if (actual is { Length: 16 } && actual.All(Uri.IsHexDigit))
        {
            var ascii = System.Text.Encoding.ASCII.GetString(Convert.FromHexString(actual));
            if (ascii.All(Uri.IsHexDigit))
            {
                actual = ascii;
            }
        }
        if (release.GitHash is { Length: >= 8 } git && actual is { Length: >= 8 } &&
            !git.AsSpan(0, 8).Equals(actual.AsSpan(0, 8), StringComparison.OrdinalIgnoreCase))
        {
            throw new FirmwareVerificationException("Post-flash source identity differs from the selected release.");
        }
    }
}
