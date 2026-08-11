using MissionPlanner.Firmware.Downloads;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Preparation;

/// <summary>Implements the presentation-neutral, non-destructive preparation workflow.</summary>
public sealed class FirmwarePreparationService(IFirmwareArtifactDownloader downloader) : IFirmwarePreparationService
{
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
