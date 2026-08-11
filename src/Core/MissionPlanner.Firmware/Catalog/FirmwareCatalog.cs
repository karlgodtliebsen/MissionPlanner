using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Catalog;

/// <summary>Contains normalized releases and cache provenance.</summary>
public sealed record FirmwareCatalog(
    IReadOnlyList<FirmwareManifestEntry> Entries,
    DateTimeOffset RetrievedAt,
    bool IsStale,
    FirmwareManifestParseDiagnostics? ParseDiagnostics = null);
