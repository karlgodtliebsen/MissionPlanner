namespace MissionPlanner.Firmware.Catalog;

/// <summary>Contains a cacheable source manifest and HTTP validators.</summary>
public sealed record CachedFirmwareManifest(
    ReadOnlyMemory<byte> Content,
    DateTimeOffset RetrievedAt,
    string? ETag = null,
    DateTimeOffset? LastModified = null,
    Uri? SourceUri = null,
    int SchemaVersion = 1);
