using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Simulation;

/// <summary>Loads verified SITL release metadata.</summary>
public interface ISitlManifestProvider
{
    /// <summary>Gets configured or official manifest entries.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Verified release metadata.</returns>
    Task<IReadOnlyList<SitlManifestEntry>> GetReleasesAsync(CancellationToken cancellationToken = default);
}

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

/// <summary>Provides host-platform detection and safe executable version probing.</summary>
public interface ISitlPlatformService
{
    /// <summary>Gets current platform capabilities.</summary>
    SitlPlatformCapability Current { get; }

    /// <summary>Attempts to query an external SITL executable version without a shell.</summary>
    /// <param name="executablePath">Absolute configured executable path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The version output, or <see langword="null"/> when unavailable.</returns>
    Task<string?> TryQueryVersionAsync(string executablePath, CancellationToken cancellationToken = default);
}

/// <summary>Supplies the platform-local MissionPlanner-owned SITL cache root.</summary>
public interface ISitlCachePathProvider
{
    /// <summary>Gets the absolute cache root.</summary>
    string CacheRoot { get; }
}

/// <summary>Downloads, verifies, and atomically extracts a manifest release.</summary>
public interface ISitlPackageManager
{
    /// <summary>Prepares a verified cached installation.</summary>
    /// <param name="release">The selected manifest release.</param>
    /// <param name="progress">Optional download progress from zero to one.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The verified cached installation.</returns>
    Task<SitlInstallation> PrepareAsync(
        SitlManifestEntry release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>Discovers valid cached installations.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Cached installation records.</returns>
    Task<IReadOnlyList<SitlInstallation>> DiscoverCachedAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes a MissionPlanner-owned cached installation.</summary>
    /// <param name="installation">The cached installation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task RemoveAsync(SitlInstallation installation, CancellationToken cancellationToken = default);

    /// <summary>Prunes unpinned cached versions while retaining recent releases per family.</summary>
    /// <param name="pinnedInstallationIds">Installation identities referenced by profiles.</param>
    /// <param name="keepLatestPerFamily">Minimum recent cached versions retained per family.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The removed installation identities.</returns>
    Task<IReadOnlyList<string>> PruneAsync(
        IReadOnlySet<string> pinnedInstallationIds,
        int keepLatestPerFamily,
        CancellationToken cancellationToken = default);
}

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
