using MissionPlanner.Firmware.Downloads;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Images;
using MissionPlanner.Firmware.Configuration;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace MissionPlanner.Firmware.Preparation;

/// <summary>Implements the presentation-neutral, non-destructive preparation workflow.</summary>
public sealed class FirmwarePreparationService(IFirmwareArtifactDownloader downloader,
    IFirmwarePackageReader? reader = null, IFirmwareArtifactStore? store = null,
    IOptions<FirmwareOptions>? options = null) : IFirmwarePreparationService
{
    /// <inheritdoc />
    public async Task<LocalFirmwarePreparationResult> ImportAsync(Stream content, string fileName, string? originalPath = null,
        CancellationToken cancellationToken = default)
    {
        if (!Path.GetExtension(fileName).Equals(".apj", StringComparison.OrdinalIgnoreCase))
        {
            throw new FirmwarePackageException("The ArduPilot serial workflow requires an .apj application package.");
        }
        if (reader is null || store is null)
        {
            throw new InvalidOperationException("Local firmware preparation requires the package reader and artifact store.");
        }
        using var buffer = new MemoryStream();
        var block = new byte[81920];
        var maximum = options?.Value.MaximumArtifactBytes ?? 64L * 1024 * 1024;
        int count;
        while ((count = await content.ReadAsync(block, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + count > maximum)
            {
                throw new FirmwarePackageException("Local firmware exceeds the configured artifact size limit.");
            }
            await buffer.WriteAsync(block.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }
        buffer.Position = 0;
        var package = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(buffer, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
        var key = "local-" + hash;
        // The store retains source provenance separately; no invented catalogue entry is needed.
        var metadata = new FirmwareArtifactMetadata(key, new Uri($"urn:missionplanner:local:{hash}"), DateTimeOffset.UtcNow, buffer.Length, hash);
        var cached = await store.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
        var cacheHit = false;
        if (cached is not null)
        {
            await using var existing = await cached.OpenReadAsync(cancellationToken).ConfigureAwait(false);
            cacheHit = string.Equals(Convert.ToHexString(await SHA256.HashDataAsync(existing, cancellationToken).ConfigureAwait(false)), hash, StringComparison.OrdinalIgnoreCase);
        }
        if (!cacheHit)
        {
            if (cached is not null)
            {
                await store.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            }
            await using var writer = await store.CreateTemporaryAsync(key, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;
            await buffer.CopyToAsync(writer.Stream, cancellationToken).ConfigureAwait(false);
            await writer.CommitAsync(metadata, cancellationToken).ConfigureAwait(false);
        }
        return new(package, cacheHit ? cached!.Metadata : metadata, fileName, originalPath, cacheHit);
    }

    /// <inheritdoc />
    public async Task<FirmwarePreparationResult> PrepareAsync(FirmwarePreparationRequest request, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var downloaded = await downloader.DownloadAsync(request.ManifestEntry.Artifact, progress, cancellationToken).ConfigureAwait(false);
        if (downloaded.Package.BoardId != request.ManifestEntry.Target.BoardId)
        {
            throw new FirmwarePackageException($"Manifest board ID {request.ManifestEntry.Target.BoardId} does not match package board ID {downloaded.Package.BoardId}.");
        }

        var warnings = downloaded.Package.ExternalImage.IsEmpty ? [] : new[] { "Package contains an external-flash image; installation requires reported external capacity." };
        return new FirmwarePreparationResult(request.ManifestEntry, downloaded.Metadata, downloaded.Package, downloaded.Metadata.Sha256,
            downloaded.FromCache, downloaded.Metadata.CacheKey, warnings);
    }
}
