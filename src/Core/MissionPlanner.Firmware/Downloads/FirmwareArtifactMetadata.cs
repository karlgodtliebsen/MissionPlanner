namespace MissionPlanner.Firmware.Downloads;

/// <summary>Describes immutable downloaded-artifact provenance.</summary>
public sealed record FirmwareArtifactMetadata(
    string CacheKey,
    Uri SourceUri,
    DateTimeOffset DownloadedAt,
    long Size,
    string Sha256);
