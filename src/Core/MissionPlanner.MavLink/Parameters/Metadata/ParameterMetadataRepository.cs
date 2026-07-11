using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.MavLink.Parameters.Metadata;

/// <summary>
/// Repository for parameter metadata with local file caching and 7-day expiry.
/// </summary>
public sealed class ParameterMetadataRepository(
    IParameterMetadataDownloader downloader,
    IParameterMetadataParser parser,
    ILogger<ParameterMetadataRepository> logger)
    : IParameterMetadataRepository
{
    private const int CacheExpiryDays = 7;

    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MissionPlanner",
        "ParameterCache");

    private readonly ConcurrentDictionary<VehicleType, Dictionary<string, ParameterMetadata>> memoryCache = new();
    private readonly SemaphoreSlim downloadLock = new(1, 1);

    /// <inheritdoc/>
    public async Task<ParameterMetadata?> GetMetadataAsync(
        VehicleType vehicleType,
        string parameterName,
        CancellationToken cancellationToken = default)
    {
        var allMetadata = await GetAllMetadataAsync(vehicleType, cancellationToken);

        return allMetadata.TryGetValue(parameterName, out var metadata)
            ? metadata
            : null;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, ParameterMetadata>> GetAllMetadataAsync(VehicleType vehicleType, CancellationToken cancellationToken = default)
    {
        // Check memory cache first
        if (memoryCache.TryGetValue(vehicleType, out var cached))
        {
            return cached;
        }

        // Prevent concurrent downloads for the same vehicle type
        await downloadLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (memoryCache.TryGetValue(vehicleType, out cached))
            {
                return cached;
            }

            // Try to load from file cache
            var cacheFile = GetCacheFilePath(vehicleType);
            if (IsCacheValid(cacheFile))
            {
                logger.LogInformation("Loading parameter metadata for {VehicleType} from cache: {CacheFile}", vehicleType, cacheFile);

                try
                {
                    await using var fileStream = File.OpenRead(cacheFile);
                    var metadata = await parser.ParseAsync(fileStream, vehicleType, cancellationToken);
                    if (metadata.Any())
                    {
                        memoryCache[vehicleType] = metadata;
                        return metadata;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to load cached metadata for {VehicleType}, will download fresh copy", vehicleType);
                }
            }

            // Download and cache
            return await DownloadAndCacheAsync(vehicleType, cancellationToken);
        }
        finally
        {
            downloadLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task RefreshAsync(VehicleType vehicleType, CancellationToken cancellationToken = default)
    {
        await downloadLock.WaitAsync(cancellationToken);
        try
        {
            // Remove from memory cache
            memoryCache.TryRemove(vehicleType, out var _);

            // Download fresh copy
            await DownloadAndCacheAsync(vehicleType, cancellationToken);
        }
        finally
        {
            downloadLock.Release();
        }
    }

    /// <inheritdoc/>
    public void ClearCache()
    {
        memoryCache.Clear();

        try
        {
            if (Directory.Exists(CacheDirectory))
            {
                Directory.Delete(CacheDirectory, true);
                logger.LogInformation("Cleared parameter metadata cache directory");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete cache directory");
        }
    }

    private async Task<Dictionary<string, ParameterMetadata>> DownloadAndCacheAsync(VehicleType vehicleType, CancellationToken cancellationToken)
    {
        logger.LogInformation("Downloading fresh metadata for {VehicleType}", vehicleType);

        await using var stream = await downloader.DownloadAsync(vehicleType, cancellationToken);

        // Parse the metadata
        var metadata = await parser.ParseAsync(stream, vehicleType, cancellationToken);

        // Save to file cache
        await SaveToCacheAsync(vehicleType, stream, cancellationToken);

        // Update memory cache
        memoryCache[vehicleType] = metadata;

        return metadata;
    }

    private async Task SaveToCacheAsync(VehicleType vehicleType, Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            var cacheFile = GetCacheFilePath(vehicleType);
            var directory = Path.GetDirectoryName(cacheFile);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Reset stream position
            stream.Position = 0;

            await using var fileStream = File.Create(cacheFile);
            await stream.CopyToAsync(fileStream, cancellationToken);

            logger.LogInformation("Saved parameter metadata for {VehicleType} to cache: {CacheFile}", vehicleType, cacheFile);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to save metadata to cache for {VehicleType}, will continue without file cache",
                vehicleType);
        }
    }

    private static string GetCacheFilePath(VehicleType vehicleType)
    {
        return Path.Combine(CacheDirectory, $"{vehicleType}_metadata.xml");
    }

    private bool IsCacheValid(string cacheFile)
    {
        if (!File.Exists(cacheFile))
        {
            return false;
        }

        var fileInfo = new FileInfo(cacheFile);
        var age = DateTimeOffset.UtcNow - fileInfo.LastWriteTimeUtc;

        if (age.TotalDays > CacheExpiryDays)
        {
            logger.LogInformation("Cache file {CacheFile} is {Days:F1} days old (expired after {ExpiryDays} days)", cacheFile, age.TotalDays, CacheExpiryDays);
            return false;
        }

        return true;
    }
}
