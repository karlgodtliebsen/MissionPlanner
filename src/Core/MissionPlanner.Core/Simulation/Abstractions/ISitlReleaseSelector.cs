using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Simulation;

namespace MissionPlanner.Core.Simulation;

/// <summary>Selects host-compatible SITL releases.</summary>
public interface ISitlReleaseSelector
{
    /// <summary>Filters releases by family, channel, and host capability.</summary>
    /// <param name="releases">Manifest releases.</param>
    /// <param name="family">Requested firmware family.</param>
    /// <param name="channel">Requested release channel.</param>
    /// <param name="capability">Detected host capability.</param>
    /// <returns>Compatible releases ordered newest first.</returns>
    IReadOnlyList<SitlManifestEntry> Select(
        IEnumerable<SitlManifestEntry> releases,
        FirmwareFamily family,
        FirmwareReleaseChannel channel,
        SitlPlatformCapability capability);
}
