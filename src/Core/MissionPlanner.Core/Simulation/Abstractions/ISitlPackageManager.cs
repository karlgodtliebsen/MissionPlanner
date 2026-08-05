namespace MissionPlanner.Core.Simulation;

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
