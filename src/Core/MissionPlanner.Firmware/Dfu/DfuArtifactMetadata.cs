namespace MissionPlanner.Firmware.Dfu;

/// <summary>Contains inspected Intel HEX metadata and provenance.</summary>
public sealed record DfuArtifactMetadata(
    long SourceBytes,
    long DataBytes,
    uint LowestAddress,
    uint HighestAddress,
    string Sha256,
    IReadOnlyList<DfuMemoryRange> Ranges,
    IReadOnlyList<string> Warnings,
    uint? EntryAddress = null,
    bool AppearsToContainBootloader = false,
    DateTimeOffset? InspectedAt = null,
    bool AppearsToContainApplication = false);
