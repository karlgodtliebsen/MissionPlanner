using MissionPlanner.Firmware.Downloads;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;

namespace MissionPlanner.Firmware.Preparation;

/// <summary>Defines a non-destructive firmware download and validation request.</summary>
public sealed record FirmwarePreparationRequest(FirmwareManifestEntry ManifestEntry);

/// <summary>Contains validated package, provenance, and cache evidence.</summary>
public sealed record FirmwarePreparationResult(
    FirmwareManifestEntry ManifestEntry,
    FirmwareArtifactMetadata ArtifactMetadata,
    ApjFirmwarePackage Package,
    string Sha256,
    bool WasCacheHit,
    string CacheIdentity,
    IReadOnlyList<string> Warnings)
{
    /// <summary>Gets the package board ID.</summary>
    public int PackageBoardId => Package.BoardId;
    /// <summary>Gets the decoded internal image size.</summary>
    public long InternalImageSize => Package.Image.Length;
    /// <summary>Gets the decoded external image size.</summary>
    public long ExternalImageSize => Package.ExternalImage.Length;
}

/// <summary>Downloads and validates firmware without accessing hardware.</summary>
public interface IFirmwarePreparationService
{
    /// <summary>Prepares a selected manifest artifact for later installation.</summary>
    Task<FirmwarePreparationResult> PrepareAsync(FirmwarePreparationRequest request, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default);
}

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
        return new(request.ManifestEntry, downloaded.Metadata, downloaded.Package, downloaded.Metadata.Sha256,
            downloaded.FromCache, downloaded.Metadata.CacheKey, warnings);
    }
}
