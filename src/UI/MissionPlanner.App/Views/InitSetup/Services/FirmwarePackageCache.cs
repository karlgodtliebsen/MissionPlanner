using MissionPlanner.Core.Firmware;

namespace MissionPlanner.App.Views.InitSetup.Services;

/// <summary>Stores verified firmware downloads beneath the OS cache directory.</summary>
public sealed class FirmwarePackageCache : IFirmwarePackageCache
{
    private readonly string cacheDirectory = Path.Combine(FileSystem.CacheDirectory, "firmware");

    /// <inheritdoc />
    public string GetPath(string cacheKey)
    {
        ValidateKey(cacheKey);
        return Path.Combine(cacheDirectory, cacheKey);
    }

    /// <inheritdoc />
    public Task<Stream?> OpenReadAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        var path = GetPath(cacheKey);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan)
            : null;
        return Task.FromResult(stream);
    }

    /// <inheritdoc />
    public async Task<string> SaveAsync(string cacheKey, Stream content, CancellationToken cancellationToken = default)
    {
        var path = GetPath(cacheKey);
        Directory.CreateDirectory(cacheDirectory);
        var temporaryPath = Path.Combine(cacheDirectory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var target = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await content.CopyToAsync(target, cancellationToken);
            }

            File.Move(temporaryPath, path, true);
            return path;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <inheritdoc />
    public void Remove(string cacheKey)
    {
        var path = GetPath(cacheKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void ValidateKey(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey) || Path.GetFileName(cacheKey) != cacheKey)
        {
            throw new ArgumentException("Firmware cache key must be a plain file name.", nameof(cacheKey));
        }
    }
}
