namespace MissionPlanner.Firmware.Catalog;

/// <summary>Summarizes accepted and independently skipped manifest entries.</summary>
public sealed record FirmwareManifestParseDiagnostics(
    int TotalEntries,
    int AcceptedEntries,
    int SkippedEntries,
    IReadOnlyDictionary<string, int> SkipReasons);
