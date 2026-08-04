using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Catalog;

/// <summary>Defines deterministic catalogue filtering and refresh behavior.</summary>
public sealed record FirmwareCatalogRequest(
    FirmwareVehicleType? VehicleType = null,
    FirmwareReleaseChannel? Channel = null,
    int? BoardId = null,
    UsbIdentifier? UsbIdentifier = null,
    bool ForceRefresh = false);

/// <summary>Contains normalized releases and cache provenance.</summary>
public sealed record FirmwareCatalog(
    IReadOnlyList<FirmwareManifestEntry> Entries,
    DateTimeOffset RetrievedAt,
    bool IsStale);

/// <summary>Contains a cacheable source manifest and HTTP validators.</summary>
public sealed record CachedFirmwareManifest(
    ReadOnlyMemory<byte> Content,
    DateTimeOffset RetrievedAt,
    string? ETag = null,
    DateTimeOffset? LastModified = null,
    Uri? SourceUri = null,
    int SchemaVersion = 1);

/// <summary>Represents a conditional manifest response.</summary>
public sealed record FirmwareManifestResponse(
    ReadOnlyMemory<byte> Content,
    bool NotModified,
    string? ETag = null,
    DateTimeOffset? LastModified = null);
