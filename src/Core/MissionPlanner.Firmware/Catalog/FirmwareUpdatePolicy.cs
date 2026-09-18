using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Catalog;

/// <summary>Conservative stable-release matching shared by connected-vehicle update checks.</summary>
public static class FirmwareUpdatePolicy
{
    /// <summary>Finds a newer stable APJ only when the reported platform and build variant identify one exact target.</summary>
    public static FirmwareManifestEntry? FindUpdate(VehicleFirmwareIdentity identity, string platform,
        IEnumerable<FirmwareManifestEntry> entries)
    {
        if (identity.Autopilot != 3 || identity.FlightVersion is not { ReleaseType: FirmwareReleaseType.Official } installed)
        {
            return null;
        }

        var family = identity.Family switch
        {
            FirmwareFamily.ArduCopter => FirmwareVehicleType.Copter,
            FirmwareFamily.ArduPlane => FirmwareVehicleType.Plane,
            FirmwareFamily.Rover => FirmwareVehicleType.Rover,
            FirmwareFamily.ArduSub => FirmwareVehicleType.Sub,
            FirmwareFamily.AntennaTracker => FirmwareVehicleType.AntennaTracker,
            FirmwareFamily.Blimp => FirmwareVehicleType.Blimp,
            _ => FirmwareVehicleType.Unknown
        };
        var variant = identity.MavType == 4 ? FirmwareVehicleType.Helicopter : family;
        var candidates = FirmwareTargetSelector.Query(entries,
                new FirmwareTargetQuery(VehicleFamily: family, ReleaseChannel: FirmwareReleaseChannel.Stable, Platform: platform))
            .Select(item => item.Entry)
            .Where(entry => string.Equals(entry.Target.Platform, platform, StringComparison.Ordinal) &&
                entry.Target.MavType == variant && entry.Artifact.Format == FirmwareImageFormat.Apj)
            .ToArray();
        // A name shared by conflicting board identities is not sufficient evidence.
        if (family == FirmwareVehicleType.Unknown || candidates.Select(entry => entry.Target.BoardId).Distinct().Count() != 1)
        {
            return null;
        }

        var current = new Version(installed.Major, installed.Minor, installed.Patch);
        var newer = candidates.Where(entry => entry.Version.SemanticVersion is { } version &&
                new Version(version.Major, version.Minor, Math.Max(0, version.Build)) > current)
            .OrderByDescending(entry => entry.Version.SemanticVersion).ToArray();
        if (newer.Length == 0)
        {
            return null;
        }
        var best = newer[0];
        return newer.Where(entry => entry.Version.SemanticVersion == best.Version.SemanticVersion)
            .Select(entry => entry.Artifact.DownloadUri).Distinct().Count() == 1 ? best : null;
    }
}
