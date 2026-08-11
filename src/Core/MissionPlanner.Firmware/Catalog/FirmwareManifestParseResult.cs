using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Catalog;

/// <summary>Contains normalized entries and parser diagnostics.</summary>
public sealed record FirmwareManifestParseResult(
    IReadOnlyList<FirmwareManifestEntry> Entries,
    FirmwareManifestParseDiagnostics Diagnostics);
