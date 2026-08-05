using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Simulation;

namespace MissionPlanner.Core.Simulation;

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
