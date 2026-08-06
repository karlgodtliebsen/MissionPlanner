using MissionPlanner.Firmware;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Simulation;

/// <summary>Describes one verified SITL release artifact.</summary>
/// <param name="Family">Compatible ArduPilot firmware family.</param>
/// <param name="Platform">Artifact platform.</param>
/// <param name="Architecture">Artifact architecture.</param>
/// <param name="Version">Release version.</param>
/// <param name="Channel">Release channel.</param>
/// <param name="DownloadUri">Absolute HTTPS archive URI.</param>
/// <param name="Sha256">Expected archive SHA-256 digest.</param>
/// <param name="ArchiveFormat">Verified archive format.</param>
/// <param name="ExecutableRelativePath">Executable path relative to the archive root.</param>
/// <param name="PublishedAt">Release publication time.</param>
public sealed record SitlManifestEntry(
    FirmwareFamily Family,
    SitlPlatform Platform,
    SitlArchitecture Architecture,
    string Version,
    FirmwareReleaseChannel Channel,
    Uri DownloadUri,
    string Sha256,
    SitlArchiveFormat ArchiveFormat,
    string ExecutableRelativePath,
    DateTimeOffset PublishedAt)
{
    /// <summary>Gets a concise release label.</summary>
    public string DisplayName => $"{Version} — {Channel} — {Platform}/{Architecture}";
}
