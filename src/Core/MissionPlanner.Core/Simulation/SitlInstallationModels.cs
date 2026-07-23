using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Simulation;

/// <summary>Identifies a supported SITL host platform.</summary>
public enum SitlPlatform
{
    /// <summary>Native Windows executable.</summary>
    Windows,

    /// <summary>Native Linux executable.</summary>
    Linux,

    /// <summary>Linux executable hosted by Windows Subsystem for Linux.</summary>
    WindowsSubsystemForLinux,

    /// <summary>Native macOS executable.</summary>
    MacOS
}

/// <summary>Identifies a supported SITL CPU architecture.</summary>
public enum SitlArchitecture
{
    /// <summary>64-bit x86.</summary>
    X64,

    /// <summary>64-bit ARM.</summary>
    Arm64
}

/// <summary>Identifies a verified SITL package archive format.</summary>
public enum SitlArchiveFormat
{
    /// <summary>ZIP archive.</summary>
    Zip,

    /// <summary>GZip-compressed POSIX tar archive.</summary>
    TarGzip
}

/// <summary>Identifies who owns a SITL installation.</summary>
public enum SitlInstallationSource
{
    /// <summary>User-selected external installation that MissionPlanner must never remove.</summary>
    External,

    /// <summary>Verified versioned cache owned by MissionPlanner.</summary>
    VerifiedCache
}

/// <summary>Identifies current installation availability.</summary>
public enum SitlInstallationState
{
    /// <summary>The executable is present and compatible with this host.</summary>
    Available,

    /// <summary>The selected installation is absent.</summary>
    Missing,

    /// <summary>The installation exists but targets another host platform or architecture.</summary>
    Incompatible,

    /// <summary>The cached installation failed integrity or structure validation.</summary>
    Corrupt
}

/// <summary>Describes current SITL host capabilities.</summary>
/// <param name="Platform">Detected host/runtime platform.</param>
/// <param name="Architecture">Detected process architecture.</param>
/// <param name="CanExecuteNative">Whether native SITL execution is supported.</param>
/// <param name="Message">Capability explanation.</param>
public sealed record SitlPlatformCapability(
    SitlPlatform Platform,
    SitlArchitecture Architecture,
    bool CanExecuteNative,
    string Message);

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

/// <summary>Describes one discovered or cached SITL installation.</summary>
/// <param name="InstallationId">Stable installation identity.</param>
/// <param name="Family">Supported firmware family.</param>
/// <param name="Platform">Installation platform.</param>
/// <param name="Architecture">Installation architecture.</param>
/// <param name="Version">Detected or manifest version.</param>
/// <param name="ExecutablePath">Absolute executable path.</param>
/// <param name="Source">Installation ownership.</param>
/// <param name="State">Current availability.</param>
/// <param name="CacheKey">MissionPlanner cache key, when owned.</param>
/// <param name="Message">Availability detail.</param>
/// <param name="PublishedAt">Manifest publication time for cached releases.</param>
public sealed record SitlInstallation(
    string InstallationId,
    FirmwareFamily Family,
    SitlPlatform Platform,
    SitlArchitecture Architecture,
    string Version,
    string ExecutablePath,
    SitlInstallationSource Source,
    SitlInstallationState State,
    string? CacheKey,
    string Message,
    DateTimeOffset? PublishedAt = null)
{
    /// <summary>Gets a concise installation label.</summary>
    public string DisplayName => $"{Family} {Version} — {Source} — {State}";
}

/// <summary>Describes how a pinned profile resolves to an installation.</summary>
/// <param name="State">Resolution state.</param>
/// <param name="Installation">Resolved installation, when available.</param>
/// <param name="Message">Resolution detail.</param>
public sealed record SitlInstallationResolution(
    SitlInstallationState State,
    SitlInstallation? Installation,
    string Message);

/// <summary>Configures a user-selected external SITL installation.</summary>
public sealed class ExternalSitlInstallationOptions
{
    /// <summary>Gets or sets the firmware family.</summary>
    public FirmwareFamily Family { get; set; }

    /// <summary>Gets or sets an optional version label used when probing is unavailable.</summary>
    public string? Version { get; set; }

    /// <summary>Gets or sets the absolute executable path.</summary>
    public string ExecutablePath { get; set; } = string.Empty;
}

/// <summary>Configures verified SITL manifests, external installations, and extraction limits.</summary>
public sealed class SitlManifestOptions
{
    /// <summary>Application configuration section.</summary>
    public const string SectionName = "Sitl";

    /// <summary>Gets or sets the optional official HTTPS manifest URL.</summary>
    public string? ManifestUrl { get; set; }

    /// <summary>Gets or sets statically configured verified releases.</summary>
    public List<SitlManifestEntry> Releases { get; set; } = [];

    /// <summary>Gets or sets user-configured external installations.</summary>
    public List<ExternalSitlInstallationOptions> ExternalInstallations { get; set; } = [];

    /// <summary>Gets or sets the maximum accepted archive size.</summary>
    public long MaximumArchiveBytes { get; set; } = 1024L * 1024 * 1024;

    /// <summary>Gets or sets the maximum total extracted size.</summary>
    public long MaximumExtractedBytes { get; set; } = 4L * 1024 * 1024 * 1024;
}
