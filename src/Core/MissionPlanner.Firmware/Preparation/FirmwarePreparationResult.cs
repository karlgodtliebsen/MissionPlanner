using MissionPlanner.Firmware.Downloads;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Preparation;

/// <summary>Contains validated package, provenance, and cache evidence.</summary>
public sealed record FirmwarePreparationResult(
    FirmwareManifestEntry ManifestEntry,
    FirmwareArtifactMetadata ArtifactMetadata,
    ApjFirmwarePackage Package,
    string Sha256,
    bool WasCacheHit,
    string CacheIdentity,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Gets the package board ID.</summary>
    public int PackageBoardId => Package.BoardId;

    /// <summary>Gets the decoded internal image size.</summary>
    public long InternalImageSize => Package.Image.Length;

    /// <summary>Gets the decoded external image size.</summary>
    public long ExternalImageSize => Package.ExternalImage.Length;
}
