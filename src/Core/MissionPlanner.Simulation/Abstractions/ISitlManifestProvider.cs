namespace MissionPlanner.Simulation.Abstractions;

/// <summary>Loads verified SITL release metadata.</summary>
public interface ISitlManifestProvider
{
    /// <summary>Gets configured or official manifest entries.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Verified release metadata.</returns>
    Task<IReadOnlyList<SitlManifestEntry>> GetReleasesAsync(CancellationToken cancellationToken = default);
}
