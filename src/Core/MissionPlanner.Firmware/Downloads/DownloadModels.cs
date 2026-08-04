using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Downloads;

/// <summary>Describes immutable downloaded-artifact provenance.</summary>
public sealed record FirmwareArtifactMetadata(
    string CacheKey,
    Uri SourceUri,
    DateTimeOffset DownloadedAt,
    long Size,
    string Sha256);

/// <summary>Contains a validated stored artifact and parsed package.</summary>
public sealed record DownloadedFirmwareArtifact(
    IFirmwareStoredArtifact StoredArtifact,
    ApjFirmwarePackage Package,
    FirmwareArtifactMetadata Metadata,
    bool FromCache);

/// <summary>Describes one removable cached artifact without exposing a required platform path.</summary>
public sealed record FirmwareArtifactCacheEntry(FirmwareArtifactMetadata Metadata, string? DiagnosticPath = null);
