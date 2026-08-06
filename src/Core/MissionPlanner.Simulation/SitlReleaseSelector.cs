using MissionPlanner.Firmware;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Simulation.Abstractions;

namespace MissionPlanner.Simulation;

/// <summary>Selects exact family, channel, platform, and architecture SITL artifacts.</summary>
public sealed class SitlReleaseSelector : ISitlReleaseSelector
{
    /// <inheritdoc />
    public IReadOnlyList<SitlManifestEntry> Select(
        IEnumerable<SitlManifestEntry> releases,
        FirmwareFamily family,
        FirmwareReleaseChannel channel,
        SitlPlatformCapability capability)
    {
        ArgumentNullException.ThrowIfNull(releases);
        ArgumentNullException.ThrowIfNull(capability);
        return !capability.CanExecuteNative
            ? []
            : (IReadOnlyList<SitlManifestEntry>)releases.Where(release =>
                    release.Family == family &&
                    release.Channel == channel &&
                    release.Platform == capability.Platform &&
                    release.Architecture == capability.Architecture)
                .OrderByDescending(release => release.PublishedAt)
                .ThenByDescending(release => release.Version, StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }
}
