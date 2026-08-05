using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Simulation;

/// <summary>Discovers external/cached installations and resolves profile version pins.</summary>
public interface ISitlInstallationService
{
    /// <summary>Discovers configured external and verified cached installations.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>All known installations.</returns>
    Task<IReadOnlyList<SitlInstallation>> DiscoverAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets manifest releases compatible with a family and channel.</summary>
    /// <param name="family">Firmware family.</param>
    /// <param name="channel">Release channel.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Compatible releases.</returns>
    Task<IReadOnlyList<SitlManifestEntry>> GetReleasesAsync(
        FirmwareFamily family,
        FirmwareReleaseChannel channel,
        CancellationToken cancellationToken = default);

    /// <summary>Downloads and installs one verified manifest release.</summary>
    /// <param name="release">Manifest release.</param>
    /// <param name="progress">Optional progress.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The installed release.</returns>
    Task<SitlInstallation> InstallAsync(
        SitlManifestEntry release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a MissionPlanner-owned cached installation.</summary>
    /// <param name="installation">The installation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RemoveAsync(SitlInstallation installation, CancellationToken cancellationToken = default);

    /// <summary>Resolves an exact profile installation/version pin.</summary>
    /// <param name="profile">Simulator profile.</param>
    /// <param name="installations">Known installations.</param>
    /// <returns>Resolution state.</returns>
    SitlInstallationResolution Resolve(
        SimulatorProfile profile,
        IReadOnlyList<SitlInstallation> installations);
}
