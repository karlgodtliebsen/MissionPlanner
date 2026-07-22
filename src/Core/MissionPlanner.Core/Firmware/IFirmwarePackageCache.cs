namespace MissionPlanner.Core.Firmware;

/// <summary>Stores downloaded firmware packages in a platform-appropriate local cache.</summary>
public interface IFirmwarePackageCache
{
    /// <summary>Resolves a safe cache key to its platform-local path.</summary>
    /// <param name="cacheKey">The safe cache key.</param>
    /// <returns>The local path.</returns>
    string GetPath(string cacheKey);

    /// <summary>Opens a cached package for reading.</summary>
    /// <param name="cacheKey">The safe cache key.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The package stream, or <see langword="null"/> when absent.</returns>
    Task<Stream?> OpenReadAsync(string cacheKey, CancellationToken cancellationToken = default);

    /// <summary>Stores a downloaded package.</summary>
    /// <param name="cacheKey">The safe cache key.</param>
    /// <param name="content">The package content.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The platform-local package path passed to a flashing adapter.</returns>
    Task<string> SaveAsync(string cacheKey, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Removes an invalid cached package.</summary>
    /// <param name="cacheKey">The safe cache key.</param>
    void Remove(string cacheKey);
}
