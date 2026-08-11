namespace MissionPlanner.Firmware.Downloads;

/// <summary>Describes one removable cached artifact without exposing a required platform path.</summary>
public sealed record FirmwareArtifactCacheEntry(FirmwareArtifactMetadata Metadata, string? DiagnosticPath = null);
