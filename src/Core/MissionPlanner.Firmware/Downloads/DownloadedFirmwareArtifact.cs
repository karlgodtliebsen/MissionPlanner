using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Downloads;

/// <summary>Contains a validated stored artifact and parsed package.</summary>
public sealed record DownloadedFirmwareArtifact(
    IFirmwareStoredArtifact StoredArtifact,
    ApjFirmwarePackage Package,
    FirmwareArtifactMetadata Metadata,
    bool FromCache);
