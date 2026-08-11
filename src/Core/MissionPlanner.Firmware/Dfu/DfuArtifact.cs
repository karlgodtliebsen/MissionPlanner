namespace MissionPlanner.Firmware.Dfu;

/// <summary>Represents a locally available, inspected DFU artifact.</summary>
public sealed record DfuArtifact(
    string FileName,
    string LocalPath,
    DfuArtifactMetadata Metadata,
    Uri? SourceUri = null,
    string? Platform = null,
    int? BoardId = null);
