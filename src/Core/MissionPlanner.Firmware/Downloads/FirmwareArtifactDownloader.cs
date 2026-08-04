using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Configuration;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Images;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Downloads;

/// <summary>Streams artifacts through bounded validation into atomic storage.</summary>
public sealed class FirmwareArtifactDownloader(
    HttpClient httpClient,
    IFirmwareArtifactStore store,
    IFirmwarePackageReader packageReader,
    IOptions<FirmwareOptions> options,
    TimeProvider timeProvider) : IFirmwareArtifactDownloader
{
    /// <inheritdoc />
    public async Task<DownloadedFirmwareArtifact> DownloadAsync(
        FirmwareArtifact artifact,
        IProgress<FirmwareProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (!options.Value.AllowInsecureArtifactUrls && artifact.DownloadUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new FirmwareDownloadException("Firmware artifacts require HTTPS.");
        }

        var cacheKey = CacheKey(artifact);
        var cached = await store.TryGetAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            try
            {
                var cachedResult = await ValidateStoredAsync(cached, artifact, true, cancellationToken).ConfigureAwait(false);
                if (cachedResult is not null)
                {
                    return cachedResult;
                }
            }
            catch (Exception exception) when (exception is FirmwarePackageException or IOException or CryptographicException)
            {
                // A cache entry is an optimization, never an authority. Redownload if its bytes no longer validate.
            }
        }

        await using var writer = await store.CreateTemporaryAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        try
        {
            using var response = await httpClient.GetAsync(artifact.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var declared = response.Content.Headers.ContentLength;
            var limit = artifact.Size is { } expectedSize
                ? Math.Min(options.Value.MaximumArtifactBytes, expectedSize)
                : options.Value.MaximumArtifactBytes;
            if (declared > limit)
            {
                throw new FirmwareDownloadException("Firmware artifact exceeds the configured size limit.");
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                total = checked(total + read);
                if (total > limit)
                {
                    throw new FirmwareDownloadException("Firmware artifact exceeded the configured size limit while streaming.");
                }

                await writer.Stream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                hash.AppendData(buffer, 0, read);
                progress?.Report(new FirmwareProgress(FirmwareOperationState.Downloading, declared is > 0 ? total * 100d / declared : null, "download.progress", total, declared));
            }

            if (artifact.Size is { } exactSize && total != exactSize)
            {
                throw new FirmwareDownloadException($"Downloaded size {total} does not match declared artifact size {exactSize}.");
            }

            var sha = Convert.ToHexString(hash.GetHashAndReset());
            if (artifact.Sha256 is not null && !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(artifact.Sha256), Convert.FromHexString(sha)))
            {
                throw new FirmwareDownloadException("Firmware artifact SHA-256 verification failed.");
            }

            writer.Stream.Position = 0;
            _ = await packageReader.ReadAsync(writer.Stream, cancellationToken).ConfigureAwait(false);
            var metadata = new FirmwareArtifactMetadata(cacheKey, artifact.DownloadUri, timeProvider.GetUtcNow(), total, sha);
            var stored = await writer.CommitAsync(metadata, cancellationToken).ConfigureAwait(false);
            return (await ValidateStoredAsync(stored, artifact, false, cancellationToken).ConfigureAwait(false))!;
        }
        catch (OperationCanceledException) { throw; }
        catch (FirmwareDownloadException) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or IOException or FirmwarePackageException or CryptographicException)
        {
            throw new FirmwareDownloadException("Firmware artifact download or validation failed.", exception);
        }
    }

    private async Task<DownloadedFirmwareArtifact?> ValidateStoredAsync(IFirmwareStoredArtifact stored, FirmwareArtifact expected, bool fromCache, CancellationToken cancellationToken)
    {
        if ((expected.Size is { } exactSize && stored.Metadata.Size != exactSize) ||
            (expected.Sha256 is not null && !string.Equals(stored.Metadata.Sha256, expected.Sha256, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        await using var stream = await stored.OpenReadAsync(cancellationToken).ConfigureAwait(false);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            hash.AppendData(buffer, 0, read);
        }

        var actualHash = Convert.ToHexString(hash.GetHashAndReset());
        if (!string.Equals(actualHash, stored.Metadata.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!stream.CanSeek)
        {
            throw new IOException("Stored artifact stream must support validation rewind.");
        }

        stream.Position = 0;
        var package = await packageReader.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        return new DownloadedFirmwareArtifact(stored, package, stored.Metadata, fromCache);
    }

    private static string CacheKey(FirmwareArtifact artifact)
    {
        var identity = $"{artifact.DownloadUri.AbsoluteUri}|{artifact.Size}|{artifact.Sha256}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }
}
