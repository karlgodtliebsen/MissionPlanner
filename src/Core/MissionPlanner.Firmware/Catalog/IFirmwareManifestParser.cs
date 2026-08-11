using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Catalog;

/// <summary>Parses source manifest bytes into normalized releases.</summary>
public interface IFirmwareManifestParser
{
    /// <summary>Parses a plain or gzip-compressed manifest.</summary>
    IReadOnlyList<FirmwareManifestEntry> Parse(ReadOnlyMemory<byte> content);

    /// <summary>Parses a manifest while reporting isolated entry failures.</summary>
    FirmwareManifestParseResult ParseWithDiagnostics(ReadOnlyMemory<byte> content);
}
