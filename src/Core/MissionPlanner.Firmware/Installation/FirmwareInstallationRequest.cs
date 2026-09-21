using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Compatibility;

namespace MissionPlanner.Firmware.Installation;

/// <summary>Defines one disconnected application-firmware installation.</summary>
public sealed record FirmwareInstallationRequest(
    BootloaderEntryContext EntryContext,
    FirmwareArtifact? Artifact = null,
    ApjFirmwarePackage? Package = null,
    FirmwareInstallationSource Source = FirmwareInstallationSource.OfficialCatalogue,
    FirmwareCompatibilityPolicy? CompatibilityPolicy = null,
    string? LocalFileName = null)
{
    /// <summary>Explicit normal upgrade or target recovery intent.</summary>
    public FirmwareInstallMode Mode { get; init; }

    /// <summary>Selected catalogue identity requiring normal reconnect and post-flash verification.</summary>
    public FirmwareManifestEntry? ExpectedRelease { get; init; }
}
