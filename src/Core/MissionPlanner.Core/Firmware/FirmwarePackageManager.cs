using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.Core.Firmware;

/// <summary>Provides HTTPS download, local caching, and SHA-256 package verification.</summary>
public sealed class FirmwarePackageManager(
    IHttpClientFactory httpClientFactory,
    IFirmwarePackageCache cache,
    ILogger<FirmwarePackageManager> logger) : IFirmwarePackageManager
{
    /// <inheritdoc />
    public async Task<FirmwarePackage> PrepareAsync(
        FirmwareManifestEntry release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRelease(release);
        var cacheKey = CreateCacheKey(release);
        var cached = await cache.OpenReadAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            await using (cached)
            {
                var cachedHash = await ComputeHashAsync(cached, cancellationToken).ConfigureAwait(false);
                if (HashMatches(cachedHash, release.Sha256))
                {
                    progress?.Report(1);
                    return new FirmwarePackage(release, cache.GetPath(cacheKey), cachedHash, true);
                }
            }

            logger.LogWarning("Removing firmware cache entry {CacheKey} after checksum mismatch.", cacheKey);
            cache.Remove(cacheKey);
        }

        logger.LogInformation("Downloading firmware {Version} for target {BoardTarget}.", release.Version, release.BoardTarget);
        var client = httpClientFactory.CreateClient("Firmware");
        using var response = await client.GetAsync(release.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var length = response.Content.Headers.ContentLength;
        await using var network = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var buffer = new MemoryStream();
        var bytes = new byte[81920];
        long received = 0;
        int read;
        while ((read = await network.ReadAsync(bytes, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await buffer.WriteAsync(bytes.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            received += read;
            if (length is > 0)
            {
                progress?.Report(Math.Clamp((double)received / length.Value, 0, 1));
            }
        }

        buffer.Position = 0;
        var computed = await ComputeHashAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (!HashMatches(computed, release.Sha256))
        {
            throw new InvalidDataException($"Firmware checksum mismatch. Expected {release.Sha256}, computed {computed}.");
        }

        buffer.Position = 0;
        var localPath = await cache.SaveAsync(cacheKey, buffer, cancellationToken).ConfigureAwait(false);
        progress?.Report(1);
        logger.LogInformation("Verified firmware {Version} for target {BoardTarget} with SHA-256 {Sha256}.", release.Version, release.BoardTarget, computed);
        return new FirmwarePackage(release, localPath, computed, true);
    }

    private static void ValidateRelease(FirmwareManifestEntry release)
    {
        if (!release.DownloadUri.IsAbsoluteUri || release.DownloadUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Firmware packages must use an absolute HTTPS URI.");
        }

        if (release.Sha256.Length != 64 || !release.Sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("Firmware manifest SHA-256 must contain exactly 64 hexadecimal characters.");
        }
    }

    private static string CreateCacheKey(FirmwareManifestEntry release)
    {
        var identity = $"{release.BoardTarget}|{release.Version}|{release.DownloadUri}";
        return $"firmware-{Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity))).ToLowerInvariant()}.bin";
    }

    private static async Task<string> ComputeHashAsync(Stream stream, CancellationToken cancellationToken)
    {
        var digest = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(digest).ToLower(CultureInfo.InvariantCulture);
    }

    private static bool HashMatches(string computed, string expected) =>
        string.Equals(computed, expected, StringComparison.OrdinalIgnoreCase);
}
