namespace MissionPlanner.Firmware.Downloads;

/// <summary>Abstracts temporary and reusable artifact storage.</summary>
public interface IFirmwareArtifactStore
{
    /// <summary>Gets a previously committed artifact by immutable key.</summary>
    Task<IFirmwareStoredArtifact?> TryGetAsync(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>Creates a temporary writer invisible to cache readers.</summary>
    Task<IFirmwareArtifactWriter> CreateTemporaryAsync(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>Enumerates valid committed cache entries.</summary>
    Task<IReadOnlyList<FirmwareArtifactCacheEntry>> EnumerateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<FirmwareArtifactCacheEntry>>([]);
    }

    /// <summary>Removes one committed cache entry.</summary>
    Task<bool> RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    /// <summary>Removes partial, corrupt, expired, and over-quota entries.</summary>
    Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
