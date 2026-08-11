namespace MissionPlanner.Firmware.Catalog;

/// <summary>Represents a conditional manifest response.</summary>
public sealed record FirmwareManifestResponse(
    ReadOnlyMemory<byte> Content,
    bool NotModified,
    string? ETag = null,
    DateTimeOffset? LastModified = null);
